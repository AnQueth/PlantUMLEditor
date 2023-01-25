using PlantUMLEditor.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class UnknownDocumentModel : TextDocumentModel
    {
        private readonly Action<UMLDiagram, UMLDiagram> ChangedCallback;


        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {
            PreviewDiagramModel? imageModel = new PreviewDiagramModel(base._ioService, base._jarLocation, Title);

            Preview? previewWindow = new Preview
            {
                DataContext = imageModel
            };

            return (imageModel, previewWindow);

        }

        public UnknownDocumentModel(Action<UMLDiagram, UMLDiagram> changedCallback, IConfiguration configuration,
                        IIOService openDirectoryService,
                        UMLUnknownDiagram model, UMLDocumentCollection diagrams, string fileName, string title, string content,
                         AutoResetEvent messageCheckerTrigger
                        ) : base(configuration, openDirectoryService, fileName, title, content, messageCheckerTrigger)
        {
            ChangedCallback = changedCallback;
            Diagram = model;
            Diagrams = diagrams;
            colorCodingProvider = new UMLColorCoding();
            indenter = new Indenter();
        }

        public UMLUnknownDiagram Diagram
        {
            get; internal set;
        }
        public UMLDocumentCollection Diagrams
        {
            get; internal set;
        }

        private readonly Indenter indenter;

        private readonly UMLColorCoding colorCodingProvider;

        protected override async void ContentChanged(string text)
        {
            UMLDiagramTypeDiscovery discovery = new();
            (UMLClassDiagram? cd, UMLSequenceDiagram? sd, UMLUnknownDiagram? ud) = await UMLDiagramTypeDiscovery.TryCreateDiagram(Diagrams, text);
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

        protected override IIndenter GetIndenter()
        {
            return indenter;
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