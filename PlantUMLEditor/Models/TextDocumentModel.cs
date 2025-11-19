using Markdig;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;
using OpenAI.Chat;
using PlantUMLEditor.Controls;
using PlantUMLEditorAI;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Shapes;
using System.Xml.Serialization;
using UMLModels;

namespace PlantUMLEditor.Models
{

    internal abstract class TextDocumentModel : BaseDocumentModel, IAutoCompleteCallback
    {
        protected readonly IIOService _ioService;

        private bool _binding;

        private IAutoComplete? _autoComplete;

        private readonly TemporarySave _temporarySave;

        private string? _findText;


        private int _lineNumber;
        private ITextEditor? _textEditor;
        private readonly AutoResetEvent _messageCheckerTrigger;
        private string _textValue = string.Empty;

        private IPreviewModel? imageModel;

        private Window? previewWindow;


        protected Action? _bindedAction;

        public class ChatMessage(bool isUser) : INotifyPropertyChanged
        {

            private bool _isBusy = true;
            public bool IsBusy
            {
                get
                {
                    return _isBusy;
                }
                set
                {
                    _isBusy = value;
                    OnPropertyChanged(nameof(IsBusy));
                }
            }

            public class ToolCall
            {
                // allow nullable since some tool result objects may not have all fields
                public string? Id { get; set; }
                public string? ToolName { get; set; }
                public string? Arguments { get; set; }
                public string? Result { get; set; }

            }

            public bool IsUser { get; } = isUser;

            public ObservableCollection<ToolCall> ToolCalls { get; } = new ObservableCollection<ToolCall>();

            public FlowDocument Document
            {
                get
                {
                    MarkdownPipeline? pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                
                    return  Markdig.Wpf.Markdown.ToFlowDocument(Message, pipeline);

                }
            }


            private string _message = string.Empty;
            public string Message
            {
                get => _message;
                set
                {
                    _message = value;
                    OnPropertyChanged(nameof(Message));
                    OnPropertyChanged(nameof(Document));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged(string name)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
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
                _chatText = value;
                (SendChatCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();

                PropertyChangedInvoke(nameof(ChatText));

            }
        }


        public IAsyncCommand SendChatCommand { get; init; }

        private ChatClientAgentThread _convThread = null;

        [Description("Replaces all occurrences of 'text' with 'newText' in the document.")]
        public void ReplaceText([Description("the text to find")] string text, [Description("the new text")] string newText)
        {
            var t = this.TextEditor.TextRead();
            t = t.Replace(text, newText);
            this.TextEditor.TextWrite(t, false, GetColorCodingProvider(), GetIndenter());
        }

        [Description("Inserts the specified text at the given position.")]
        public void InsertTextAtPosition([Description("position in the original text to insert at")] int position, [Description("the text to insert")] string text)
        {
            this.TextEditor.InsertTextAt(text, position, text.Length);
        }

        [Description("rewrite the complete document")]
        public void RewriteDocument([Description("the new text for the document")] string text)
        {
            this.TextEditor.TextWrite(text, false, GetColorCodingProvider(), GetIndenter());
        }

        [Description("reads the current text in the document.")]
        public string ReadDocumentText()
        {
            return this.TextEditor.TextRead();
        }

        private async Task SendChat()
        {
            AIAgentFactory factory = new AIAgentFactory();
            var agent = factory.Create(new AISettings()
            {
                Deployment = AppSettings.Default.AzureAIDeployment,
                Endpoint = AppSettings.Default.AzureAIEndpoint,
                Key = AppSettings.Default.AzureAIKey,
                SourceName = "PlantUML"
            }, new Delegate[] {
                ReplaceText,
                InsertTextAtPosition,
                RewriteDocument,
                ReadDocumentText
            });

            if (_convThread == null)
            {
                _convThread = (ChatClientAgentThread)agent.GetNewThread();
            }
            AIConversation.Add(new ChatMessage(true)
            {
                Message = ChatText,
                IsBusy = false

            });
            ChatMessage cm = new ChatMessage(false);

            AIConversation.Add(cm);

            var prompt = $"User input: \n{ChatText}";


            ChatText = string.Empty;

            try
            {
                await foreach (var item in agent.RunStreamingAsync(prompt, _convThread))
                {
                    foreach(var c in item.Contents)
                    {
                        if(c is FunctionCallContent fc)
                        {
                            cm.ToolCalls.Add(new ChatMessage.ToolCall
                            {
                                 ToolName = fc.Name,
                                 Arguments = JsonSerializer.Serialize(fc.Arguments)
                            });
                        }
                    }
                 
                    cm.Message += item;
                }

            }
            catch (Exception ex)
            {
                cm.Message += $"\n\nError: {ex.Message}";
            }

            cm.IsBusy = false;




        }


        public TextDocumentModel(IIOService openDirectoryService,
            string fileName, string title, string content, AutoResetEvent messageCheckerTrigger) : base(fileName, title)
        {
            SendChatCommand = new AsyncDelegateCommand(SendChat, () => ChatText.Length > 0);


            _messageCheckerTrigger = messageCheckerTrigger;
            _textValue = content;

            _ioService = openDirectoryService;


            ShowPreviewCommand = new DelegateCommand(ShowPreviewCommandHandler);
            MatchingAutoCompletes = new List<string>();
            SortedMatchingAutoCompletes = new ObservableCollection<string>();
            RegenDocument = new DelegateCommand(RegenDocumentHandler);
            _temporarySave = new TemporarySave(fileName);

            string? tmpContent = _temporarySave.ReadIfExists();
            if (tmpContent != null)
            {
                _textValue = tmpContent;
                IsDirty = true;
            }

        }

        protected ITextEditor? TextEditor => _textEditor;

        public string Content
        {
            get => _textValue;
            set
            {
                _textValue = value;
                _textEditor?.TextWrite(value, false, GetColorCodingProvider(), GetIndenter());
            }
        }




        internal abstract void AutoComplete(AutoCompleteParameters autoCompleteParameters);

        public List<string> MatchingAutoCompletes
        {
            get;
        }



        public DelegateCommand? RegenDocument
        {
            get;
            private set;
        }

        public DelegateCommand? ShowPreviewCommand
        {
            get;
            private set;
        }

        public ObservableCollection<string> SortedMatchingAutoCompletes
        {
            get;
        }

        internal void InsertAtCursor(string imageMD)
        {

            _textEditor?.InsertTextAtCursor(imageMD);
        }

        protected abstract (IPreviewModel? model, Window? window) GetPreviewView();

        private async void ShowPreviewCommandHandler()
        {
            if (previewWindow is not null && imageModel is not null)
            {
                imageModel.Stop();
                previewWindow.Close();


            }

            (IPreviewModel? model, Window? window) res = GetPreviewView();
            imageModel = res.model;
            previewWindow = res.window;
            if (previewWindow is null)
            {
                return;
            }

            previewWindow.Closed += PreviewWindow_Closed;

            previewWindow.Show();
            await ShowPreviewImage(_textEditor?.TextRead());
        }

        private void PreviewWindow_Closed(object? sender, EventArgs e)
        {
            TryClosePreview();
        }

        private async Task ShowPreviewImage(string? text)
        {
            if (string.IsNullOrEmpty(text) || imageModel is null)
            {
                return;
            }

            string tmp = System.IO.Path.GetTempFileName();
            await File.WriteAllTextAsync(tmp, text);

            imageModel?.Show(tmp, Title.Trim('\"'), true);
        }

        protected virtual string AppendAutoComplete(string selection)
        {
            return selection;
        }

        protected virtual async Task ContentChanged(string text)
        {
            IsDirty = true;
            _textValue = text;
            _messageCheckerTrigger.Set();

            //_mostUsedWords.Clear();
            //foreach (Match m in words.Matches(text))
            //{
            //    var s = m.Value;

            //    if (_mostUsedWords.ContainsKey(s))
            //    {
            //        _mostUsedWords[s]++;
            //    }
            //    else
            //        _mostUsedWords.Add(s, 1);
            //}

            if (previewWindow != null)
            {
                await ShowPreviewImage(text);
            }

            _temporarySave.Save(text);

        }

        protected virtual void RegenDocumentHandler()
        {
        }

        protected void ShowAutoComplete()
        {
            SortedMatchingAutoCompletes.Clear();

            //var s = (from o in MatchingAutoCompletes
            //         join a in _mostUsedWords.Keys on o equals a
            //         select new { o, c = _mostUsedWords[o] }).ToList();

            //foreach (var f in s.OrderByDescending(p => p.c))
            //    SortedMatchingAutoCompletes.Add(f.o);

            //foreach (var item in MatchingAutoCompletes.Where(z => !s.Any(p => p.o == z)).OrderBy(p => p))
            //    SortedMatchingAutoCompletes.Add(item);

            foreach (string? item in MatchingAutoCompletes.OrderBy(p => p))
            {
                SortedMatchingAutoCompletes.Add(item);
            }

            _autoComplete?.ShowAutoComplete(this);
        }

        protected abstract IIndenter GetIndenter();

        protected abstract IColorCodingProvider? GetColorCodingProvider();

        internal void Binded(ITextEditor textEditor)
        {
            _textEditor = textEditor;
            _autoComplete = (IAutoComplete)textEditor;
            _binding = true;

            _textEditor.TextWrite(_textValue, false, GetColorCodingProvider(), GetIndenter());


            _binding = false;
            _bindedAction?.Invoke();

            TextEditor?.GotoLine(_lineNumber, _findText);

            _messageCheckerTrigger.Set();
        }

        internal void CloseAutoComplete()
        {
            if (_autoComplete != null)
            {
                _autoComplete.CloseAutoComplete();
            }
        }

        internal void ReportMessage(DocumentMessage d)
        {
            _textEditor?.ReportError(d.LineNumber, 0);
        }

        internal async Task Save()
        {
            await File.WriteAllTextAsync(FileName, Content);
            IsDirty = false;
            _temporarySave.Clean();
        }

        internal void TryClosePreview()
        {
            if (previewWindow != null)
            {
                previewWindow.Closed -= PreviewWindow_Closed;

                if (imageModel != null)
                {
                    imageModel.Stop();
                    imageModel = null;
                }



                previewWindow.Close();
                previewWindow.DataContext = null;
                previewWindow = null;

            }
        }



        public override void Close()
        {
            _temporarySave.Stop();
            _temporarySave.Clean();
            Visible = Visibility.Collapsed;
            imageModel?.Stop();
            if (previewWindow != null)
            {

                TryClosePreview();


            }
            _autoComplete = null;
            _textEditor?.Destroy();
            _textEditor = null;
            imageModel = null;
            ShowPreviewCommand = null;
            RegenDocument = null;


        }

        public abstract Task<UMLDiagram?> GetEditedDiagram();

        public void GotoLineNumber(int lineNumber, string? findText)
        {
            _lineNumber = lineNumber;
            _findText = findText;
            if (TextEditor != null)
            {
                TextEditor.GotoLine(_lineNumber, findText);
            }
        }

        public virtual void NewAutoComplete(string text)
        {
        }

        public void Selection(string selection, AutoCompleteParameters autoCompleteParameters)
        {
            if (autoCompleteParameters == null)
            {
                return;
            }

            selection = AppendAutoComplete(selection);

            _textEditor?.InsertTextAt(selection, autoCompleteParameters.IndexInText, autoCompleteParameters.TypedLength);
        }

        public void SetActive()
        {
            Visible = Visibility.Visible;
        }

        public void TextChanged(string text)
        {
            if (_binding)
            {
                return;
            }

            ContentChanged(text).ConfigureAwait(false);

        }

    }
}