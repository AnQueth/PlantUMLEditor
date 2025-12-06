using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PlantUMLEditorAI;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
        async ValueTask<object?> ConfirmFunctionCallMiddleware(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
        {

            if (context.Arguments.Count == 0)
            {
                return await next(context, cancellationToken);

            }

            StringBuilder sb = new StringBuilder();
            foreach (var arg in context!.Arguments)
            {
                if (arg.Key is not null)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{arg.Key}: {arg.Value ?? "null"}");

            }

            var canCall = await ConfirmCanRun(context!.Function.Name, sb.ToString());
            if (!canCall)
            {

                context.Terminate = true;
                return null;
            }

            return await next(context, cancellationToken);



        }
        private string _chatText = string.Empty;
        private ChatClientAgentThread? _convThread = null;
        private bool _isCheckingCommandCanRun;

        public ObservableCollection<ChatMessage> AIConversation { get; } = new ObservableCollection<ChatMessage>();

        public string ChatText
        {
            get
            {
                return _chatText;
            }
            set
            {
                SetValue(ref _chatText, value);

                (SendChatCommand as AsyncDelegateCommand<BaseDocumentModel>)?.RaiseCanExecuteChanged();
            }
        }

        private void NewChatCommandHandler()
        {
            AIConversation.Clear();
            _convThread = null;
        }

        private async Task OpenDocument(string pathToFile)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await AttemptOpeningFile(pathToFile);
            });
        }

        public bool IsCheckingCommandCanRun
        {
            get => _isCheckingCommandCanRun;
            set => SetValue(ref _isCheckingCommandCanRun, value);
        }

        private string? _tryingToRun;
        public string? TryingToRun
        {
            get => _tryingToRun;
            set => SetValue(ref _tryingToRun, value);
        }

        private string? _tryingToRunParameters;
        public string? TryingToRunParameters
        {
            get => _tryingToRunParameters;
            set => SetValue(ref _tryingToRunParameters, value);
        }
        public ICommand CanRunCommand
        {
            get;
            init;
        }

        public void CanRunCommandHandler(bool? canRun)
        {
            IsCheckingCommandCanRun = false;
            _canRunTcs?.SetResult(canRun.GetValueOrDefault());
        }

        private TaskCompletionSource<bool>? _canRunTcs;

        private async Task<bool> ConfirmCanRun(string name, string parameters)
        {
            TryingToRun = name;
            TryingToRunParameters = parameters;

            _canRunTcs = new TaskCompletionSource<bool>();
            IsCheckingCommandCanRun = true;
            return await _canRunTcs.Task;



        }

        private async Task SendChat(BaseDocumentModel currentDoc)
        {
            var currentMessage = new ChatMessage(false);

            AIConversation.Add(new ChatMessage(true)
            {
                Message = ChatText,
                IsBusy = false
            });
            AIConversation.Add(currentMessage);

            if (string.IsNullOrEmpty(FolderBase))
            {
                currentMessage.Message = "No folder is currently open.";
                currentMessage.IsBusy = false;
                return;
            }

            AIAgent agent;

            var settings = new AISettings()
            {
                Deployment = AppSettings.Default.AzureAIDeployment,
                Endpoint = AppSettings.Default.AzureAIEndpoint,
                Key = AppSettings.Default.AzureAIKey,
                MaxOutputTokens = AppSettings.Default.AzureAIMaxOutputTokens,
                SourceName = "PlantUML"
            };

            if (currentDoc is TextDocumentModel tdm)
            {
                var aiTools = new AIToolsEditable(tdm, currentMessage, OpenDocument, FolderBase);

                AIAgentFactory factory = new AIAgentFactory();
                agent = factory.Create(settings, ConfirmFunctionCallMiddleware,
                    new Delegate[] {
                        aiTools.ReplaceText,
                        aiTools.InsertTextAtPosition,
                        aiTools.RewriteDocument,

                        aiTools.SearchInAllDocuments,
                               aiTools.ReadFileByPath,
                        aiTools.CreateNewDocument,
                        aiTools.ReadDocumentText,
                        aiTools.FetchUrlContent
                });
            }
            else if (currentDoc is IScriptable scriptable)
            {
                var aiTools = new AIToolsScriptable(scriptable, OpenDocument,
                    FolderBase);

                AIAgentFactory factory = new AIAgentFactory();
                agent = factory.Create(settings, ConfirmFunctionCallMiddleware, new Delegate[]
                {
                        aiTools.SearchInAllDocuments,
                        aiTools.CreateNewDocument,
                        aiTools.ReadDocumentText,
                        aiTools.FetchUrlContent,
                               aiTools.ReadFileByPath,
                        aiTools.ExecuteScript
                });
            }
            else if (CurrentDocument is ITextGetter textGetter)
            {
                var aiTools = new AIToolsTextGetter(textGetter, OpenDocument, FolderBase);

                AIAgentFactory factory = new AIAgentFactory();
                agent = factory.Create(settings, ConfirmFunctionCallMiddleware,
                    new Delegate[] {
                        aiTools.SearchInAllDocuments,

                        aiTools.CreateNewDocument,
                        aiTools.ReadDocumentText,
                        aiTools.FetchUrlContent,
                               aiTools.ReadFileByPath
                });
            }
            else
            {
                var aiTools = new AIToolsBasic(OpenDocument, FolderBase);

                AIAgentFactory factory = new AIAgentFactory();
                agent = factory.Create(settings, ConfirmFunctionCallMiddleware,
                    new Delegate[] {
                        aiTools.SearchInAllDocuments,
                        aiTools.ReadFileByPath,
                        aiTools.CreateNewDocument,
                });
            }

            if (_convThread == null)
            {
                _convThread = (ChatClientAgentThread)agent.GetNewThread();
            }

            var prompt = $"User input: \n{ChatText}";

            ChatText = string.Empty;

            try
            {
                await foreach (var item in agent.RunStreamingAsync(prompt, _convThread))
                {
                    foreach (var c in item.Contents)
                    {
                        if (c is FunctionCallContent fc)
                        {
                            currentMessage.ToolCalls.Add(new ChatMessage.ToolCall
                            {
                                ToolName = fc.Name,
                                Arguments = System.Text.Json.JsonSerializer.Serialize(fc.Arguments)
                            });
                        }
                    }

                    currentMessage.Message += item;
                }
            }
            catch (Exception ex)
            {
                currentMessage.Message += $"\n\nError: {ex.Message}";
            }

            currentMessage.IsBusy = false;
        }

        private async Task UndoEditCommandHandler(ObservableCollection<UndoOperation> list)
        {
            var first = list.First();
            await AttemptOpeningFile(first.fileName);
            if (CurrentDocument is null)
                return;
            TextDocumentModel? tdm = CurrentDocument as TextDocumentModel;
            if (tdm is not null)
                tdm.Content = first.textBefore;
        }
    }
}