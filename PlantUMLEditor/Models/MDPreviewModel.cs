using Neo.Markdig.Xaml;
using Prism.Mvvm;
using System.IO;
using System.Windows.Documents;

namespace PlantUMLEditor.Models
{
    internal class MDPreviewModel : BindableBase, IPreviewModel
    {
        private FlowDocument _document;


        public string Title
        {
            get;
            init;
        }

        public MDPreviewModel(string title)
        {
            Title = title;
        }

        public FlowDocument Document
        {
            get => _document;
            set => SetProperty(ref _document, value);
        }

        public void Show(string path, string name, bool delete)
        {
            var markdown = File.ReadAllText(path);
            Document = MarkdownXaml.ToFlowDocument(markdown);

        }


        public void Stop()
        {

        }
    }
}
