using PlantUMLEditor.Controls;
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

    internal abstract class TextDocumentModel : BaseDocumentModel, IAutoCompleteCallback
    {
        protected readonly IIOService _ioService;
        protected readonly string _jarLocation;

        private IAutoComplete? _autoComplete;

        private readonly TemporarySave _temporarySave;

        private string? _findText;


        private int _lineNumber;
        private ITextEditor? _textEditor;

        private string _textValue = string.Empty;

        private IPreviewModel? imageModel;

        private Window? previewWindow;
        private readonly Visibility visible;

        protected Action? _bindedAction;




        public TextDocumentModel(IConfiguration configuration, IIOService openDirectoryService,
            string fileName, string title, string content) : base(fileName, title)
        {




            Content = content;

            _ioService = openDirectoryService;
            _jarLocation = configuration.JarLocation;

            ShowPreviewCommand = new DelegateCommand(ShowPreviewCommandHandler);
            MatchingAutoCompletes = new List<string>();
            SortedMatchingAutoCompletes = new ObservableCollection<string>();
            RegenDocument = new DelegateCommand(RegenDocumentHandler);
            _temporarySave = new TemporarySave(fileName);

            string? tmpContent = _temporarySave.ReadIfExists();
            if (tmpContent != null)
            {
                Content = tmpContent;
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
                _textEditor?.TextWrite(value, false, GetColorCodingProvider());
            }
        }




        internal abstract void AutoComplete(AutoCompleteParameters autoCompleteParameters);

        public List<string> MatchingAutoCompletes
        {
            get;
        }



        public DelegateCommand RegenDocument
        {
            get;
            private set;
        }

        public DelegateCommand ShowPreviewCommand
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

            _textEditor.InsertTextAtCursor(imageMD);
        }

        protected abstract (IPreviewModel? model, Window? window) GetPreviewView();

        private async void ShowPreviewCommandHandler()
        {
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

            string tmp = Path.GetTempFileName();
            await File.WriteAllTextAsync(tmp, text);

            imageModel?.Show(tmp, Title.Trim('\"'), true);
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

        protected abstract IColorCodingProvider? GetColorCodingProvider();

        internal void Binded(ITextEditor textEditor)
        {
            _textEditor = textEditor;
            _autoComplete = (IAutoComplete)textEditor;

            _textEditor.TextWrite(_textValue, false, GetColorCodingProvider());

            _bindedAction?.Invoke();

            TextEditor?.GotoLine(_lineNumber, _findText);
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
            if (_textValue != text)
            {
                _textValue = text;
                ContentChanged(text);
            }
        }

    }
}