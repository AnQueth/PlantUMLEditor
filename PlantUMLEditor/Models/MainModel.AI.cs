using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PlantUMLEditorAI;
using Prism.Commands;
using System.Windows;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {



        public ObservableCollection<ChatMessage> AIConversation { get; } = new ObservableCollection<ChatMessage>();

        private string _chatText = string.Empty;

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

        private ChatClientAgentThread? _convThread = null;

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

        private void NewChatCommandHandler()
        {
            AIConversation.Clear();
            _convThread = null;
        }

        private async Task SendChat(BaseDocumentModel baseDocumentModel)
        {
            var currentMessage = new ChatMessage(false);

            AIConversation.Add(new ChatMessage(true)
            {
                Message = ChatText,
                IsBusy = false

            });
            AIConversation.Add(currentMessage);

            var currentDoc = baseDocumentModel as ITextGetter;
            if (currentDoc is null)
            {
                currentMessage.Message = "No text document is currently open to interact with the AI.";
                currentMessage.IsBusy = false;
                return;
            }

            if (string.IsNullOrEmpty(FolderBase))
            {
                currentMessage.Message = "No folder is currently open.";
                currentMessage.IsBusy = false;
                return;
            }

            AIAgent agent;
        

            if (currentDoc is TextDocumentModel tdm)
            {
                var aiTools = new AIToolsEditable(tdm, currentMessage,
                    async (string folderBase) =>
                    {
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await ScanDirectory(folderBase);
                        });
                    },
                    async (string pathToFile) =>
                    {
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await AttemptOpeningFile(pathToFile);
                        });
                    },
                    FolderBase);

                AIAgentFactory factory = new AIAgentFactory();
                agent = factory.Create(new AISettings()
                {
                    Deployment = AppSettings.Default.AzureAIDeployment,
                    Endpoint = AppSettings.Default.AzureAIEndpoint,
                    Key = AppSettings.Default.AzureAIKey,
                    MaxOutputTokens = AppSettings.Default.AzureAIMaxOutputTokens,
                    SourceName = "PlantUML"
                }, new Delegate[] {
                        aiTools.ReplaceText,
                        aiTools.InsertTextAtPosition,
                        aiTools.RewriteDocument,
                        aiTools.SearchInDocuments,
                        aiTools.CreateNewDocument,
                        aiTools.ReadDocumentText
                }
        );
            }
            else
            {
                    var     aiTools = new AIToolsTextGetter(currentDoc,
                    async (string folderBase) =>
                    {
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await ScanDirectory(folderBase);
                        });
                    },
                    async (string pathToFile) =>
                    {
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await AttemptOpeningFile(pathToFile);
                        });
                    },
                    FolderBase);

                AIAgentFactory factory = new AIAgentFactory();
                agent = factory.Create(new AISettings()
                {
                    Deployment = AppSettings.Default.AzureAIDeployment,
                    Endpoint = AppSettings.Default.AzureAIEndpoint,
                    Key = AppSettings.Default.AzureAIKey,
                        MaxOutputTokens = AppSettings.Default.AzureAIMaxOutputTokens,
                    SourceName = "PlantUML"
                }, new Delegate[] {

                        aiTools.SearchInDocuments,
                        aiTools.CreateNewDocument,
                        aiTools.ReadDocumentText
                }
        );
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
    }
}
