using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PlantUMLEditor.Models
{
    public class DocumentModel : BindingBase, IAutoCompleteCallback
    {
        private string name;
        private int _typedLength;
        private int _lastIndex = 0;

        public bool IsDirty
        {
            get;set;

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

        public DocumentModel(IAutoComplete autoComplete, IConfiguration configuration)
        {
            _jarLocation = configuration.JarLocation;
            _autoComplete = autoComplete;
            ShowPreviewCommand = new DelegateCommand(ShowPreviewCommandHandler);
            MatchingAutoCompletes = new ObservableCollection<string>();
      
            RegenDocument = new DelegateCommand(RegenDocumentHandler);
        }

        protected virtual void RegenDocumentHandler()
        {
           
        }
 
        private PreviewDiagramModel imageModel;
        private Preview PreviewWindow;
        private string _textValue;

   

        private void ShowPreviewCommandHandler()
        {
            imageModel = new PreviewDiagramModel();

            PreviewWindow = new Preview();
            imageModel.Title = Name;

            PreviewWindow.DataContext = imageModel;

            PreviewWindow.Show();
            ShowPreviewImage();
        }

        internal void Binded()
        {
            this.TextWrite(_textValue);
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


        public void Close()
        {
            imageModel?.Stop();
            if (PreviewWindow != null)
                PreviewWindow.Close();
        }

        protected virtual void ContentChanged(string text)
        {
            IsDirty = true;


            if (PreviewWindow != null)
                ShowPreviewImage(text);
        }

        public DocumentTypes DocumentType
        {
            get; set;
        }



        public string Content
        {
            get
            {
                return _textValue;


            }
            set
            {
                
                _textValue = value;
                this.TextWrite?.Invoke(value);




            }
        }

        public virtual void AutoComplete(Rect rec, string text, int line, string word, int position, int typedLength)
        {
            _typedLength = typedLength;
            _lastIndex = position;
            this._autoComplete.CloseAutoComplete();
        }

        public void TextChanged(string text)
        {
            _textValue = text;
            ContentChanged(text);
        }

        internal void CloseAutoComplete()
        {
            this._autoComplete.CloseAutoComplete();

        }

        public string FileName
        {
            get; set;
        }

        private readonly string _jarLocation;
        private readonly IAutoComplete _autoComplete;

        public DelegateCommand ShowPreviewCommand { get; }
        public Action<string> TextWrite { get; internal set; }
        public Func<string> TextRead { get; internal set; }
        public Action<string> InsertText { get; internal set; }
        public Action<string, int, int> InsertTextAt { get; internal set; }

        public Action TextClear { get; internal set; }
        public virtual Task PrepareSave()
        {
            IsDirty = false;

            return Task.CompletedTask;
        }

        protected void ShowAutoComplete(Rect rec, bool allowTyping = false)
        {
           
            _autoComplete.FocusAutoComplete(rec, this, allowTyping);
        }

        public void Selection(string selection)
        {
            this.InsertTextAt(selection, LastIndex, LastWordLength);
        }

        public virtual void NewAutoComplete(string text)
        {
            
        }

        public ObservableCollection<string> MatchingAutoCompletes
        {
            get;
        }
 

        internal void KeyPressed()
        {
            this._autoComplete.CloseAutoComplete();
        }

        public DelegateCommand RegenDocument { get; }
        public int LastIndex { get => _lastIndex; }
        public int LastWordLength { get => _typedLength; }
    }

    public enum DocumentTypes
    {
        Class,
        Sequence
    }
}