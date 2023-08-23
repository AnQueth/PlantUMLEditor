using PlantUMLEditor.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class TextFileDocumentModel : TextDocumentModel
        {
            public TextFileDocumentModel(IConfiguration configuration, IIOService openDirectoryService,
               string fileName, string title, string content, AutoResetEvent messageCheckerTrigger) :
                base(configuration, openDirectoryService, fileName, title, content, messageCheckerTrigger)
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

            protected override IIndenter GetIndenter()
            {
                return new NullIndenter();
            }

            internal override void AutoComplete(AutoCompleteParameters autoCompleteParameters)
            {

            }
        }
    
}
