﻿using PlantUMLEditor.Controls;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class MDDocumentModel : TextDocumentModel
    {
        public MDDocumentModel(IConfiguration configuration, IIOService openDirectoryService,
          string fileName, string title, string content) :
            base(configuration, openDirectoryService, fileName, title, content)
        {

        }
        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {
            string? path = Path.GetDirectoryName(base.FileName);
            if (path is null)
            {
                return default;
            }

            MDPreviewModel? m = new MDPreviewModel(base.Title, path);
            MDPreview? p = new MDPreview
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
