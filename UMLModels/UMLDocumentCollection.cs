using System.Collections.Generic;
using System.Linq;

namespace UMLModels
{
    public class UMLDocumentCollection
    {
        public UMLDocumentCollection()
        {
            ClassDocuments = new List<UMLClassDiagram>();
            SequenceDiagrams = new List<UMLSequenceDiagram>();
        }

        public List<UMLClassDiagram> ClassDocuments { get; set; }

        public List<UMLSequenceDiagram> SequenceDiagrams { get; set; }

        public Dictionary<string, UMLDataType> DataTypes
        {
            get
            {
                return ClassDocuments.SelectMany(z => z.DataTypes).ToDictionary(p => p.Name, p => p);
            }
        }
    }
}