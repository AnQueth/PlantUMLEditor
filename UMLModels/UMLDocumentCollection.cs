using System.Collections.Generic;

namespace UMLModels
{
    public class UMLDocumentCollection
    {
        public UMLDocumentCollection()
        {
            ClassDocuments = new List<UMLClassDiagram>();
            SequenceDiagrams = new List<UMLSequenceDiagram>();
            ComponentDiagrams = new List<UMLComponentDiagram>();
        }

        public List<UMLClassDiagram> ClassDocuments { get; set; }

        public List<UMLComponentDiagram> ComponentDiagrams { get; set; }
        public List<UMLSequenceDiagram> SequenceDiagrams { get; set; }
    }
}