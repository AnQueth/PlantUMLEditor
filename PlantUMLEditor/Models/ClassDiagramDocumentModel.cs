using System;
using System.Threading.Tasks;
using System.Windows.Documents;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class ClassDiagramDocumentModel : DocumentModel
    {
        private readonly Action<UMLClassDiagram, UMLClassDiagram> ChangedCallback = null;

        public ClassDiagramDocumentModel()
        {
        }

        public ClassDiagramDocumentModel(Action< UMLClassDiagram, UMLClassDiagram> changedCallback)
        {
            this.ChangedCallback = changedCallback;
        }

        public UMLClassDiagram Diagram { get; set; }

        protected override void ContentChanged(ref string text)
        {
          //  PlantUML.UMLClassDiagramParser.ReadClassDiagramString(text);

            base.ContentChanged(ref text);
        }

        public override async Task PrepareSave()
        {
            var z = await PlantUML.UMLClassDiagramParser.ReadClassDiagramString(Content);


            ChangedCallback(Diagram, z);
            Diagram = z;

            await base.PrepareSave();
        }
    }
}