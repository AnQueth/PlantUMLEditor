using Prism.Commands;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PlantUMLEditor.Models
{
    public class DocumentModel : BindingBase
    {
        private string name;





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

        public DocumentModel()
        {
            ShowPreviewCommand = new DelegateCommand(ShowPreviewCommandHandler);
        }

        private PreviewDiagramModel imageModel;
        private Preview PreviewWindow;
        private string _textValue;

        private object _lock = new object();

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
            await imageModel.ShowImage("d:\\downloads\\plantuml.jar", FileName);
        }

        private async Task ShowPreviewImage(string text)
        {
            Task.Run(async () =>
            {

               
                    string tmp = Path.GetTempFileName();
                    await File.WriteAllTextAsync(tmp, text);

                    await imageModel.ShowImage("d:\\downloads\\plantuml.jar", tmp);

                    if (File.Exists(tmp))
                        File.Delete(tmp);
               
            });
        }


       
        protected virtual void ContentChanged(string text)
        {
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

        public virtual void AutoComplete(Rect rec, string text, int line)
        {
             
        }

        public void TextChanged(string text)
        {
            _textValue = text;
            ContentChanged(text);
        }



        public string FileName
        {
            get; set;
        }
        public DelegateCommand ShowPreviewCommand { get; }
        public Action<string> TextWrite { get; internal set; }
        public Func<string> TextRead { get; internal set; }
        public Action<string> InsertText { get; internal set; }

        public virtual Task PrepareSave()
        {
            return Task.CompletedTask;
        }
    }

    public enum DocumentTypes
    {
        Class,
        Sequence
    }
}