using Prism.Commands;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public enum DocumentTypes
    {
        Class,
        Sequence,
        Unknown,
        Component
    }

    public abstract class DocumentModel : BindingBase, IAutoCompleteCallback
    {
        private readonly IIOService _ioService;
        private readonly string _jarLocation;

        private IAutoComplete _autoComplete;
        private AutoCompleteParameters _autoCompleteParameters;

        private bool _isDirty;

        private int _lineNumber;

        private ITextEditor _textEditor;

        private string _textValue;

        private PreviewDiagramModel imageModel;

        private string name;

        private Preview PreviewWindow;
        private Visibility visible;

        public DocumentModel(IConfiguration configuration, IIOService openDirectoryService)
        {
            _ioService = openDirectoryService;
            _jarLocation = configuration.JarLocation;

            ShowPreviewCommand = new DelegateCommand(ShowPreviewCommandHandler);
            MatchingAutoCompletes = new List<string>();
            SortedMatchingAutoCompletes = new ObservableCollection<string>();
            RegenDocument = new DelegateCommand(RegenDocumentHandler);
        }

        protected AutoCompleteParameters AutoCompleteParameters => _autoCompleteParameters;

        protected ITextEditor TextEditor { get => _textEditor; }

        public string Content
        {
            get
            {
                return _textValue;
            }
            set
            {
                _textValue = value;
                _textEditor?.TextWrite(value, false);
            }
        }

        public DocumentTypes DocumentType
        {
            get; set;
        }

        public string FileName
        {
            get; set;
        }

        public bool IsDirty
        {
            get
            {
                return _isDirty;
            }
            set
            {
                SetValue(ref _isDirty, value);
            }
        }

        public List<string> MatchingAutoCompletes
        {
            get;
        }

        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                SetValue(ref name, value);
            }
        }

        public DelegateCommand RegenDocument { get; }

        public DelegateCommand ShowPreviewCommand { get; }

        public ObservableCollection<string> SortedMatchingAutoCompletes
        {
            get;
        }

        public Visibility Visible
        {
            get => visible; set { SetValue(ref visible, value); }
        }

        private void ShowPreviewCommandHandler()
        {
            imageModel = new PreviewDiagramModel(_ioService);

            PreviewWindow = new Preview();
            imageModel.Title = Name;

            PreviewWindow.DataContext = imageModel;

            PreviewWindow.Show();
            ShowPreviewImage(this._textEditor.TextRead());
        }

        private async Task ShowPreviewImage(string text)
        {
            string tmp = Path.GetTempFileName();
            await File.WriteAllTextAsync(tmp, text);

            await imageModel.ShowImage(_jarLocation, tmp, true);
        }

        protected virtual string AppendAutoComplete(string selection)
        {
            return selection;
        }

        protected virtual void ContentChanged(string text)
        {
            IsDirty = true;
            _textValue = text;

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

            if (PreviewWindow != null)
                ShowPreviewImage(text);
        }

        protected virtual void RegenDocumentHandler()
        {
        }

        protected void ShowAutoComplete(Rect rec, bool allowTyping = false)
        {
            SortedMatchingAutoCompletes.Clear();

            //var s = (from o in MatchingAutoCompletes
            //         join a in _mostUsedWords.Keys on o equals a
            //         select new { o, c = _mostUsedWords[o] }).ToList();

            //foreach (var f in s.OrderByDescending(p => p.c))
            //    SortedMatchingAutoCompletes.Add(f.o);

            //foreach (var item in MatchingAutoCompletes.Where(z => !s.Any(p => p.o == z)).OrderBy(p => p))
            //    SortedMatchingAutoCompletes.Add(item);

            foreach (var item in MatchingAutoCompletes.OrderBy(p => p))
                SortedMatchingAutoCompletes.Add(item);

            _autoComplete.FocusAutoComplete(rec, this, allowTyping);
        }

        internal void Binded(ITextEditor textEditor)
        {
            _textEditor = textEditor;
            _autoComplete = textEditor as IAutoComplete;
            _textEditor.SetAutoComplete(_autoComplete);
            _textEditor.TextWrite(_textValue, false);
            this.TextEditor.GotoLine(_lineNumber);
        }

        internal void CloseAutoComplete()
        {
            this._autoComplete.CloseAutoComplete();
        }

        internal void ReportMessage(DocumentMessage d)
        {
            _textEditor?.ReportError(d.LineNumber, 0);
        }

        internal async Task Save()
        {
            await File.WriteAllTextAsync(FileName, Content);
            IsDirty = false;
        }

        public virtual void AutoComplete(AutoCompleteParameters p)
        {
            _autoCompleteParameters = p;

            //  this._autoComplete.CloseAutoComplete();
        }

        public virtual void Close()
        {
            Visible = Visibility.Collapsed;
            imageModel?.Stop();
            if (PreviewWindow != null)
                PreviewWindow.Close();
        }

        public abstract Task<UMLDiagram> GetEditedDiagram();

        public void GotoLineNumber(int lineNumber)
        {
            _lineNumber = lineNumber;
            if (this.TextEditor != null)
                this.TextEditor.GotoLine(_lineNumber);
        }

        public virtual void NewAutoComplete(string text)
        {
        }

        public void Selection(string selection)
        {
            selection = AppendAutoComplete(selection);

            _textEditor.InsertTextAt(selection, _autoCompleteParameters.CaretPosition, _autoCompleteParameters.WordLength);
        }

        public void SetActive()
        {
            Visible = Visibility.Visible;
        }

        public void TextChanged(string text)
        {
            if (_textValue != text)
            {
                _textValue = text;
                ContentChanged(text);
            }
        }
    }
}