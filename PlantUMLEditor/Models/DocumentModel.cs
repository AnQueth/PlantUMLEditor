using PlantUML;
using Prism.Commands;
using System;
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
        Unknown
    }

    public abstract class DocumentModel : BindingBase, IAutoCompleteCallback
    {
        private readonly IAutoComplete _autoComplete;
        private readonly string _jarLocation;

        private AutoCompleteParameters _autoCompleteParameters;
        private bool _isDirty;
        private int _lineNumber;
        private ITextEditor _textEditor;
        private string _textValue;
        private PreviewDiagramModel imageModel;
        private string name;
        private Preview PreviewWindow;

        public DocumentModel(IAutoComplete autoComplete, IConfiguration configuration)
        {
            _jarLocation = configuration.JarLocation;
            _autoComplete = autoComplete;
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

        private void ShowPreviewCommandHandler()
        {
            imageModel = new PreviewDiagramModel();

            PreviewWindow = new Preview();
            imageModel.Title = Name;

            PreviewWindow.DataContext = imageModel;

            PreviewWindow.Show();
            ShowPreviewImage();
        }

        private async Task ShowPreviewImage()
        {
            await imageModel.ShowImage(_jarLocation, FileName, false);
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
            if (PreviewWindow != null)
                ShowPreviewImage(text);
        }

        protected virtual void RegenDocumentHandler()
        {
        }

        protected void ShowAutoComplete(Rect rec, bool allowTyping = false)
        {
            SortedMatchingAutoCompletes.Clear();

            foreach (var item in MatchingAutoCompletes.OrderBy(p => p))
                SortedMatchingAutoCompletes.Add(item);

            _autoComplete.FocusAutoComplete(rec, this, allowTyping);
        }

        internal void Binded(ITextEditor textEditor)
        {
            _textEditor = textEditor;
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
            _textEditor.ReportError(d.LineNumber, 0);
        }

        public virtual void AutoComplete(AutoCompleteParameters p)
        {
            _autoCompleteParameters = p;

            //  this._autoComplete.CloseAutoComplete();
        }

        public void Close()
        {
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

        public void TextChanged(string text)
        {
            _textValue = text;
            ContentChanged(text);
        }
    }
}