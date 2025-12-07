using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PlantUMLEditorAI;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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
            if(context.Function is null || context.Function.UnderlyingMethod is null)
            {
                return await next(context, cancellationToken);

            }
            if (context.Function.UnderlyingMethod.GetCustomAttributes(typeof(AIToolModifyAttribute), true).Length == 0)
            {               
                return await next(context, cancellationToken);
            }

            StringBuilder sb = new StringBuilder();
            foreach (var arg in context!.Arguments)
            {
                if (arg.Key is not null)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{arg.Key}: {arg.Value ?? "null"}");

            }

            var canCall = await ConfirmCanRun(context.Function.Name, sb.ToString());
            if (!canCall)
            {

                context.Terminate = true;
                return null;
            }

            var res = await next(context, cancellationToken);

            
            return res;
        }
        private string _chatText = string.Empty;

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
            this.DeleteConversation();
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
                        aiTools.VerifyUMLFile,

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
                var aiTools = new AIToolsReadable(textGetter, OpenDocument, FolderBase);

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

            AgentThread thread;
            var threadJson = await LoadConversation();
            if (threadJson != null) 
            {
                thread = agent.DeserializeThread(threadJson.Value);
            }
            else
            {
                thread = (ChatClientAgentThread)agent.GetNewThread();
            }
                 
           

            var prompt = $"User input: \n{ChatText}";

            ChatText = string.Empty;

            try
            {
                await foreach (var item in agent.RunStreamingAsync(prompt, thread))
                {
                    foreach (var c in item.Contents)
                    {
                        if (c is FunctionCallContent fc)
                        {
                            currentMessage.ToolCalls.Add(new ChatMessage.ToolCall
                            {
                                ToolName = fc.Name,
                                Id = fc.CallId,
                                    
                                Arguments = System.Text.Json.JsonSerializer.Serialize(fc.Arguments)
                            });
                        }
                        else if(c is FunctionResultContent frc)
                        {
                            var lastCall = currentMessage.ToolCalls.LastOrDefault(tc => tc.Result is null && tc.Id == frc.CallId);
                            if (lastCall is not null)
                            {
                                lastCall.Result = frc.Result?.ToString();
                            }
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
            await SaveConversation(AIConversation, thread.Serialize());

          
        }

        private async Task SaveConversation(ObservableCollection<ChatMessage> messages, JsonElement thread)
        {
            using (IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                IsolatedStorageFileStream stream = new IsolatedStorageFileStream("conversation.json", FileMode.Create, storage);
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    string json = JsonSerializer.Serialize(new ConversationData(messages, thread ));
                    await writer.WriteAsync(json);
                }
            }
        }

        private void DeleteConversation()
        {
            using (IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                if (storage.FileExists("conversation.json"))
                {
                    storage.DeleteFile("conversation.json");
                }
            }
        }

        public async Task InitializeAI()
        {
            await LoadConversation();
        }

        private async Task<JsonElement?> LoadConversation()
        {
            using (IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                if (storage.FileExists("conversation.json"))
                {
                    IsolatedStorageFileStream stream = new IsolatedStorageFileStream("conversation.json", FileMode.Open, storage);
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string json = await reader.ReadToEndAsync();
                        try
                        {
                            var data = JsonSerializer.Deserialize<ConversationData>(json);
                            if (data is not null)
                            {
                                if (AIConversation.Count == 0)
                                {
                                    foreach (var msg in data.Messages)
                                    {
                                        AIConversation.Add(msg);

                                    }
                                }

                                return data.Thread;

                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore JSON errors and return null

                        }
                    }
                }
            }

            return null;
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

        private record ConversationData(ObservableCollection<ChatMessage> Messages, JsonElement Thread);
    }
}
