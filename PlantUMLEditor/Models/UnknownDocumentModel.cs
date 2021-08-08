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
                        IIOService openDirectoryService) : base(configuration, openDirectoryService)
        {
            this.ChangedCallback = changedCallback;
        }

        public UMLUnknownDiagram Diagram { get; internal set; }
        public UMLDocumentCollection Diagrams { get; internal set; }

        protected override async void ContentChanged(string text)
        {
            UMLDiagramTypeDiscovery discovery = new();
            var (cd, sd, ud) = await UMLDiagramTypeDiscovery.TryCreateDiagram(Diagrams, text);
            if (cd != null)
            {
                this.ChangedCallback(Diagram, cd);
            }
            else if (sd != null)
            {
                this.ChangedCallback(Diagram, sd);
            }

            base.ContentChanged(text);
        }

        public override Task<UMLDiagram?> GetEditedDiagram()
        {
            return Task.FromResult<UMLDiagram?>(Diagram);
        }
    }
}