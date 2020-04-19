using System;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class ClassDiagramDocumentModel : DocumentModel
    {
        private readonly Action<UMLClassDiagram, UMLClassDiagram> ChangedCallback = null;

        public ClassDiagramDocumentModel()
        {
        }

        public ClassDiagramDocumentModel(Action<UMLClassDiagram, UMLClassDiagram> changedCallback)
        {
            this.ChangedCallback = changedCallback;
        }

        public UMLClassDiagram Diagram { get; set; }

        protected override void ContentChanged(ref string text)
        {
            PlantUML.UMLClassDiagramParser.ReadClassDiagramString(text).ContinueWith(z =>
            {
                ChangedCallback(Diagram, z.Result);
                Diagram = z.Result;
            });

            base.ContentChanged(ref text);
        }
    }
}