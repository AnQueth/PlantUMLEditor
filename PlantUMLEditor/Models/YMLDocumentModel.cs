using PlantUMLEditor.Controls;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class YMLDocumentModel : TextDocumentModel
    {
        public YMLDocumentModel(IConfiguration configuration, IIOService openDirectoryService,
           string fileName, string title, string content) :
            base(configuration, openDirectoryService, fileName, title, content)
        {

        }
        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {

            return (null, null);

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
