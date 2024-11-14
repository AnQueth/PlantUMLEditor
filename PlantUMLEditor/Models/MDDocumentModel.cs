using PlantUMLEditor.Controls;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class MDDocumentModel : TextDocumentModel
    {
        private readonly MDColorCoding _colorCoding;
        private readonly IIndenter _indenter;
        public MDDocumentModel(  IIOService openDirectoryService,
          string fileName, string title, string content, AutoResetEvent messageCheckerTrigger) :
            base( openDirectoryService, fileName, title, content, messageCheckerTrigger)
        {
            _colorCoding = new MDColorCoding();
            _indenter = new NullIndenter();
        }

        public override void Close()
        {
            base.Close();


        }

        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {
            string? path = Path.GetDirectoryName(base.FileName);
            if (path is null)
            {
                return default;
            }

            MDPreviewModel? m = new MDPreviewModel(base.Title, path);
            MDPreviewWindow? p = new MDPreviewWindow
            {
                DataContext = m
            };
            return (m, p);

        }
        public override Task<UMLDiagram?> GetEditedDiagram()
        {
            return Task.FromResult(default(UMLDiagram?));
        }

        protected override IColorCodingProvider? GetColorCodingProvider()
        {
            return _colorCoding;
        }

        protected override IIndenter GetIndenter()
        {
            return _indenter;
        }

        internal override void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {

        }
    }
}
