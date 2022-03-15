using PlantUMLEditor.Controls;
using System;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class UnknownDocumentModel : DocumentModel
    {
        private readonly Action<UMLDiagram, UMLDiagram> ChangedCallback;


        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {
            var imageModel = new PreviewDiagramModel(base._ioService, base._jarLocation, Name);

            var previewWindow = new Preview
            {
                DataContext = imageModel
            };

            return (imageModel, previewWindow);

        }

        public UnknownDocumentModel(Action<UMLDiagram, UMLDiagram> changedCallback, IConfiguration configuration,
                        IIOService openDirectoryService,
                        UMLUnknownDiagram model, UMLDocumentCollection diagrams, string fileName, string title, string content
                        ) : base(configuration, openDirectoryService, DocumentTypes.Unknown, fileName, title, content)
        {
            ChangedCallback = changedCallback;
            Diagram = model;
            Diagrams = diagrams;
            colorCodingProvider = new UMLColorCoding();
        }

        public UMLUnknownDiagram Diagram
        {
            get; internal set;
        }
        public UMLDocumentCollection Diagrams
        {
            get; internal set;
        }

        private readonly UMLColorCoding colorCodingProvider;

        protected override async void ContentChanged(string text)
        {
            UMLDiagramTypeDiscovery discovery = new();
            var (cd, sd, ud) = await UMLDiagramTypeDiscovery.TryCreateDiagram(Diagrams, text);
            if (cd != null)
            {
                ChangedCallback(Diagram, cd);
            }
            else if (sd != null)
            {
                ChangedCallback(Diagram, sd);
            }

            base.ContentChanged(text);
        }

        protected override IColorCodingProvider? GetColorCodingProvider()
        {
            return colorCodingProvider;
        }

        public override Task<UMLDiagram?> GetEditedDiagram()
        {
            return Task.FromResult<UMLDiagram?>(Diagram);
        }

        internal override void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {

        }
    }
}