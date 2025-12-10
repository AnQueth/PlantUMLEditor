using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using PlantUMLEditorAI;
using Prism.Commands;
using System;
using System.Collections.Generic;
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
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
        // AI-related command properties
        public IAsyncCommand SendChatCommand { get; init; }
        public DelegateCommand CancelAIProcessingCommand { get; private set; }
        public ICommand PasteFromClipboardCommand { get; }
        public ICommand UndoAIEditsCommand { get; init; }
        public ICommand AddAttachmentCommand { get; }
        public ICommand AttachmentDoubleClickCommand { get; }
        public ICommand NewChatCommand { get; init; }
        public ICommand CanRunCommand { get; init; }
        public DelegateCommand EditPromptsCommand { get; }

        private void EditPromptsCommandHandler()
        {
            var win = new PromptEditorWindow();
            PromptViewModel vm = new(_promptStorage);
            win.DataContext = vm;
            win.ShowDialog();
        }
        async ValueTask<object?> ConfirmFunctionCallMiddleware(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
        {
            if (context.Function is null || context.Function.UnderlyingMethod is null)
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

        public ObservableCollection<PromptItem> Prompts { get => _promptStorage.Prompts; }

        private PromptItem _selectedPrompt;
        public PromptItem SelectedPrompt
        {
            get => _selectedPrompt ?? new PromptItem()
            {
                Name = PromptStorage.SystemPromptkey,
                Content = PromptStorage.SystemPrompt
            };
            set
            {
                _selectedPrompt = value;
                SetValue(ref _selectedPrompt, value);
            }
        }

        public ObservableCollection<ChatMessage> AIConversation { get; } = new ObservableCollection<ChatMessage>();


        // add these properties to the view model that exposes IsBusy, SendChatCommand and CancelAIProcessingCommand
        public ICommand ActiveSendCommand => IsBusy ? CancelAIProcessingCommand : SendChatCommand;
        public string ActiveSendContent => IsBusy ? "Stop" : "Send";

        // ensure IsBusy setter raises property changed for these
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;



                PropertyChangedInvoke(nameof(IsBusy));
                PropertyChangedInvoke(nameof(ActiveSendCommand));
                PropertyChangedInvoke(nameof(ActiveSendContent));
            }
        }

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

        private void AttachmentDoubleClickCommandHandler(Attachment? att)
        {
            if(att is null)
                return;
            Attachments.Remove(att);
        }

        private CancellationTokenSource? _aiCancellationTokenSource = null;
        private void CancelAIProcessingHandler()
        {
            _aiCancellationTokenSource?.Cancel();
        }

        public record Attachment(string FileName, string Type, byte[] Data)
        {
            public BitmapSource Image
            {
                get{
                    if (Data is null || Data.Length == 0)
                        return null;

                    try
                    {
                        using MemoryStream ms = new MemoryStream(Data);
                        // Create a decoder that will load the image from the stream
                        BitmapDecoder decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

                        BitmapSource? frame = decoder.Frames.FirstOrDefault();
                        if (frame != null)
                        {
                            // Freeze so it can be used across threads and in WPF bindings safely
                            frame.Freeze();
                        }

                        return frame;
                    }
                    catch
                    {
                        // Decoding failed (unsupported format, corrupted data, etc.)
                        return null;
                    }
                }
            }
        }

        public ObservableCollection<Attachment> Attachments { get; } = new ObservableCollection<Attachment>();

        public async Task AddAttechmantHandler()
        {
            var file = _ioService.GetFile(".png", ".txt", ".md", ".puml", ".jpg", ".jpeg", ".bmp", ".svg");
            if (file is null)
                return;
            bool flowControl = await AddFileAttachment(file);
            if (!flowControl)
            {
                return;
            }

        }

        private async Task<bool> AddFileAttachment(string file)
        {
            string? type = GetMimeType(file);

            if (type is null)
                return false;

            Attachments.Add(new Attachment(System.IO.Path.GetFileName(file), type,
                await File.ReadAllBytesAsync(file)));
            return true;
        }

        private string? GetMimeType(string file)
        {
            string ext = Path.GetExtension(file);
            switch (ext)
            {
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".gif":
                    return "image/gif";
                case ".bmp":
                    return "image/bmp";
                case ".svg":
                    return "image/svg+xml";
                case ".txt":
                case ".md":
                case ".puml":
                    return "text/plain";
                default:
                    return null;
            }
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

            _aiCancellationTokenSource = new CancellationTokenSource();
            IsBusy = true;
            AIAgent agent;

            var settings = new AISettings()
            {
                Deployment = AppSettings.Default.AzureAIDeployment,
                Endpoint = AppSettings.Default.AzureAIEndpoint,
                Key = AppSettings.Default.AzureAIKey,
                MaxOutputTokens = AppSettings.Default.AzureAIMaxOutputTokens,
                SourceName = "PlantUML"
            };

            if(string.IsNullOrWhiteSpace( settings.Key) || string.IsNullOrWhiteSpace( settings.Endpoint) 
                || string.IsNullOrWhiteSpace(settings.Deployment))
            {
                currentMessage.Message = "AI settings are not configured. Please set up Azure AI settings in the application settings.";
                currentMessage.IsBusy = false;
                IsBusy = false;
                return;
            }

            if (currentDoc is TextDocumentModel tdm)
            {
                var aiTools = new AIToolsEditable(tdm, currentMessage, OpenDocument, FolderBase);

                AIAgentFactory factory = new AIAgentFactory();
                agent = factory.Create(settings, ConfirmFunctionCallMiddleware, SelectedPrompt.Content,
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
                agent = factory.Create(settings, ConfirmFunctionCallMiddleware, SelectedPrompt.Content, new Delegate[]
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
                agent = factory.Create(settings, ConfirmFunctionCallMiddleware, SelectedPrompt.Content,
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
                agent = factory.Create(settings, ConfirmFunctionCallMiddleware, SelectedPrompt.Content,
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

            if (CurrentDocument is ImageDocumentModel idm)
            {

                await AddFileAttachment(idm.FileName);
            }


            ChatText = string.Empty;
            currentMessage.Attachments = Attachments.ToList();

            List<AIContent> content = new List<AIContent>
            {
                new TextContent($"{prompt}")
            };

            foreach(var att in Attachments)
            {
                
                content.Add(new DataContent(att.Data, att.Type ));
            }

     

            Microsoft.Extensions.AI.ChatMessage message = new(ChatRole.User, content); 


            try
            {
                await foreach (var item in agent.RunStreamingAsync(message, thread, cancellationToken: _aiCancellationTokenSource.Token))
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
                        else if (c is FunctionResultContent frc)
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
            catch (OperationCanceledException)
            {
                currentMessage.Message += "\n\n[Processing cancelled by user]";
            }
            catch (Exception ex)
            {
                currentMessage.Message += $"\n\nError: {ex.Message}";
            }

            Attachments.Clear();
            _aiCancellationTokenSource = null;
            currentMessage.IsBusy = false;
            IsBusy = false;
            await SaveConversation(AIConversation, thread.Serialize());


        }
        private async Task PasteFromClipboardHandler(KeyEventArgs e)
        {
            if (e == null)
                return;

            // Only act on Ctrl+V — this mirrors the original code-behind logic.
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                try
                {
                    bool added = await TryAddAttachmentFromClipboardAsync();
                    if (added)
                    {
                        // prevent the default paste into the TextBox when an image was attached
                        e.Handled = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }
        }

   
        /// <summary>
        /// Reads image data (or image files) from the clipboard and adds them to the Attachments collection.
        /// Returns true if one or more attachments were added.
        /// </summary>
        public async Task<bool> TryAddAttachmentFromClipboardAsync()
        {
            // Handle file drop list (e.g. user copied files)
            if (Clipboard.ContainsFileDropList())
            {
                var list = Clipboard.GetFileDropList();
                bool any = false;
                foreach (string path in list)
                {
                    try
                    {
                        string? type = GetMimeType(path);
                        if (type is null)
                            continue;

                        byte[] bytes = await File.ReadAllBytesAsync(path);
                        Attachments.Add(new Attachment(Path.GetFileName(path), type, bytes));
                        any = true;
                    }
                    catch
                    {
                        // ignore individual failures
                    }
                }
                return any;
            }

            // Direct image in clipboard
            if (Clipboard.ContainsImage())
            {
                try
                {
                    BitmapSource? image = Clipboard.GetImage();
                    if (image == null)
                        return false;

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    using var ms = new MemoryStream();
                    encoder.Save(ms);
                    byte[] data = ms.ToArray();

                    string fileName = $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    Attachments.Add(new Attachment(fileName, "image/png", data));
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // Some apps put bitmap data under DataFormats.Bitmap
            if (Clipboard.ContainsData(DataFormats.Bitmap))
            {
                try
                {
                    var obj = Clipboard.GetData(DataFormats.Bitmap) as BitmapSource;
                    if (obj != null)
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(obj));
                        using var ms = new MemoryStream();
                        encoder.Save(ms);
                        byte[] data = ms.ToArray();

                        string fileName = $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        Attachments.Add(new Attachment(fileName, "image/png", data));
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private async Task SaveConversation(ObservableCollection<ChatMessage> messages, JsonElement thread)
        {
            using (IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                IsolatedStorageFileStream stream = new IsolatedStorageFileStream("conversation.json", FileMode.Create, storage);
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    string json = JsonSerializer.Serialize(new ConversationData(messages, thread));
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
