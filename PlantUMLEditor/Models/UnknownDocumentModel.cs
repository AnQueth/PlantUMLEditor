using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class UnknownDocumentModel : DocumentModel
    {
        private readonly Action<UMLDiagram, UMLDiagram> ChangedCallback = null;
        private IEnumerable<string> _autoCompleteItems;

        public UnknownDocumentModel(IConfiguration configuration) : base(configuration)
        {
        }

        public UnknownDocumentModel(Action<UMLDiagram, UMLDiagram> changedCallback, IConfiguration configuration)
            : base(configuration)
        {
            this.ChangedCallback = changedCallback;
        }

        public UMLUnknownDiagram Diagram { get; internal set; }
        public UMLDocumentCollection Diagrams { get; internal set; }

        protected override async void ContentChanged(string text)
        {
            UMLDiagramTypeDiscovery discovery = new UMLDiagramTypeDiscovery();
            var r = await discovery.TryCreateDiagram(Diagrams, text);
            if (r.cd != null)
            {
                this.ChangedCallback(Diagram, r.cd);
            }
            else if (r.sd != null)
            {
                this.ChangedCallback(Diagram, r.sd);
            }

            base.ContentChanged(text);
        }

        public override Task<UMLDiagram> GetEditedDiagram()
        {
            return Task.FromResult<UMLDiagram>(null);
        }
    }
}