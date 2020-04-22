using Prism.Commands;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    public class DocumentModel : BindingBase
    {
        private string name;
        private string content;

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

        private ImageModel imageModel;
        private Preview PreviewWindow;

        private void ShowPreviewCommandHandler()
        {
            imageModel = new ImageModel();

            PreviewWindow = new Preview();


            PreviewWindow.DataContext = imageModel;
           
            PreviewWindow.Show();
            ShowPreviewImage();
        }

        private async Task ShowPreviewImage()
        {
            await imageModel.ShowImage("d:\\downloads\\plantuml.jar", FileName);
        }

        private async Task ShowPreviewImage(string text)
        {
            string tmp = Path.GetTempFileName();
            await File.WriteAllTextAsync(tmp, text);

            await imageModel.ShowImage("d:\\downloads\\plantuml.jar", tmp);
    
            if(File.Exists(tmp))
                File.Delete(tmp);
        }


        public virtual void AutoComplete(object sender, System.Windows.Input.KeyEventArgs e)
        {
        }

        protected virtual void ContentChanged(ref string text)
        {
            if(PreviewWindow != null)
                ShowPreviewImage(text);
        }

        public DocumentTypes DocumentType
        {
            get; set;
        }

        public string Content
        {
            get { return content; }
            set
            {
                SetValue(ref content, value);

                ContentChanged(ref content);
            }
        }

        public string FileName
        {
            get; set;
        }
        public DelegateCommand ShowPreviewCommand { get; }

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