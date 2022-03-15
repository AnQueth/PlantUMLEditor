using PlantUMLEditor.Controls;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class MDDocumentModel : DocumentModel
    {
        public MDDocumentModel(IConfiguration configuration, IIOService openDirectoryService,
            DocumentTypes documentType, string fileName, string title, string content) :
            base(configuration, openDirectoryService, documentType, fileName, title, content)
        {

        }
        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {
            var m = new MDPreviewModel(base.Title);
            var p = new MDPreview
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
            return null;
        }

        internal override void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {

        }
    }
}
