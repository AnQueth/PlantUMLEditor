using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace PlantUMLEditor.Models
{
    public enum DocumentTypes
    {
        Class,
        Sequence
    }

    public class DocumentModel : BindingBase, IAutoCompleteCallback
    {
        private readonly IAutoComplete _autoComplete;
        private readonly string _jarLocation;

        private AutoCompleteParameters _autoCompleteParameters;
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
            MatchingAutoCompletes = new ObservableCollection<string>();

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
                _textEditor?.TextWrite(value);
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
            get; set;
        }

        public ObservableCollection<string> MatchingAutoCompletes
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

        protected virtual void ContentChanged(string text)
        {
            IsDirty = true;

            if (PreviewWindow != null)
                ShowPreviewImage(text);
        }

        protected virtual void RegenDocumentHandler()
        {
        }

        protected void ShowAutoComplete(Rect rec, bool allowTyping = false)
        {
            _autoComplete.FocusAutoComplete(rec, this, allowTyping);
        }

        internal void Binded(ITextEditor textEditor)
        {
            _textEditor = textEditor;
            _textEditor.TextWrite(_textValue);
        }

        internal void CloseAutoComplete()
        {
            this._autoComplete.CloseAutoComplete();
        }

        internal void KeyPressed()
        {
            this._autoComplete.CloseAutoComplete();
        }

        public virtual void AutoComplete(AutoCompleteParameters p)
        {
            _autoCompleteParameters = p;

            this._autoComplete.CloseAutoComplete();
        }

        public void Close()
        {
            imageModel?.Stop();
            if (PreviewWindow != null)
                PreviewWindow.Close();
        }

        public virtual void NewAutoComplete(string text)
        {
        }

        public virtual Task PrepareSave()
        {
            IsDirty = false;

            return Task.CompletedTask;
        }

        public void Selection(string selection)
        {
            _textEditor.InsertTextAt(selection, _autoCompleteParameters.CaretPosition, _autoCompleteParameters.WordLength);
        }

        public void TextChanged(string text)
        {
            _textValue = text;
            ContentChanged(text);
        }
    }
}