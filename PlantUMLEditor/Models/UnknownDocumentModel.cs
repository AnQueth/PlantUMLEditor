using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class UnknownDocumentModel : DocumentModel
    {
        private readonly Action<UMLDiagram, UMLDiagram> ChangedCallback;
      

     

        public UnknownDocumentModel(Action<UMLDiagram, UMLDiagram> changedCallback, IConfiguration configuration,
                        IIOService openDirectoryService,
                        UMLUnknownDiagram model, UMLDocumentCollection diagrams, string fileName, string title, string content
                        ) : base(configuration, openDirectoryService, DocumentTypes.Unknown, fileName, title, content)
        {
            ChangedCallback = changedCallback;
            Diagram = model;
            Diagrams = diagrams;
        }

        public UMLUnknownDiagram Diagram { get; internal set; }
        public UMLDocumentCollection Diagrams { get; internal set; }

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

        public override Task<UMLDiagram?> GetEditedDiagram()
        {
            return Task.FromResult<UMLDiagram?>(Diagram);
        }

        internal override void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {
             
        }
    }
}