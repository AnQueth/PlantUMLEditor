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
        Unknown,
        Component
    }

    public abstract class DocumentModel : BindingBase, IAutoCompleteCallback
    {
        private readonly IIOService _ioService;
        private readonly string _jarLocation;

        private IAutoComplete? _autoComplete;

        private readonly TemporarySave _temporarySave;

        private string? _findText;
        private bool _isDirty;

        private int _lineNumber;
        private ITextEditor? _textEditor;

        private string _textValue = string.Empty;

        private PreviewDiagramModel? imageModel;

        private string name;

        private Preview? previewWindow;
        private Visibility visible;

        protected Action? _bindedAction;

        public DocumentModel(IConfiguration configuration, IIOService openDirectoryService, DocumentTypes documentType, 
            string fileName, string title, string content)
        {
            DocumentType = documentType;
            name = title;
            FileName = fileName;
            Content = content;

            _ioService = openDirectoryService;
            _jarLocation = configuration.JarLocation;

            ShowPreviewCommand = new DelegateCommand(ShowPreviewCommandHandler);
            MatchingAutoCompletes = new List<string>();
            SortedMatchingAutoCompletes = new ObservableCollection<string>();
            RegenDocument = new DelegateCommand(RegenDocumentHandler);
            _temporarySave = new TemporarySave(fileName);

            string? tmpContent = _temporarySave.ReadIfExists();
            if(tmpContent != null)
            {
                Content = tmpContent;
                IsDirty = true;
            }

        }

        protected ITextEditor? TextEditor { get => _textEditor; }

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
            get; init;
        }

        public string FileName
        {
            get; init;
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

        internal abstract  void AutoComplete(AutoCompleteParameters autoCompleteParameters);

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

        private async void ShowPreviewCommandHandler()
        {
            imageModel = new PreviewDiagramModel(_ioService);

            previewWindow = new Preview();

            imageModel.Title = Name;

            previewWindow.DataContext = imageModel;

            previewWindow.Show();
            await ShowPreviewImage(_textEditor?.TextRead());
        }

        private async Task ShowPreviewImage(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            string tmp = Path.GetTempFileName();
            await File.WriteAllTextAsync(tmp, text);

            imageModel?.ShowImage(_jarLocation, tmp, Name.Trim('\"'), true);
        }

        protected virtual string AppendAutoComplete(string selection)
        {
            return selection;
        }

        protected virtual async void ContentChanged(string text)
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

            if (previewWindow != null)
                await ShowPreviewImage(text);

           _temporarySave.Save(text);

        }

        protected virtual void RegenDocumentHandler()
        {
        }

        protected void ShowAutoComplete( )
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

            _autoComplete?.ShowAutoComplete(  this);
        }

        internal void Binded(ITextEditor textEditor)
        {
            _textEditor = textEditor;
            _autoComplete = (IAutoComplete)textEditor;

            _textEditor.TextWrite(_textValue, false);

            _bindedAction?.Invoke();

            TextEditor?.GotoLine(_lineNumber, _findText);
        }

        internal void CloseAutoComplete()
        {
            if (_autoComplete != null)
                _autoComplete.CloseAutoComplete();
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
                if (imageModel != null)
                    imageModel.Stop();
                previewWindow.Close();
            }
        }

     

        public virtual void Close()
        {
            _temporarySave.Stop();
            _temporarySave.Clean();
            Visible = Visibility.Collapsed;
            imageModel?.Stop();
            if (previewWindow != null)
                previewWindow.Close();
        }

        public abstract Task<UMLDiagram?> GetEditedDiagram();

        public void GotoLineNumber(int lineNumber, string? findText)
        {
            _lineNumber = lineNumber;
            _findText = findText;
            if (TextEditor != null)
                TextEditor.GotoLine(_lineNumber, findText);
        }

        public virtual void NewAutoComplete(string text)
        {
        }

        public void   Selection(string selection, AutoCompleteParameters autoCompleteParameters)
        {
            if (autoCompleteParameters == null)
                return;

            selection = AppendAutoComplete(selection);

            _textEditor?.InsertTextAt(selection, autoCompleteParameters.Where, autoCompleteParameters.TypedLength);
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