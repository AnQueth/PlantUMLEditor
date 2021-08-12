using System.Collections.Generic;

namespace UMLModels
{
    public class UMLSequenceDiagram : UMLDiagram
    {
    

        public UMLSequenceDiagram(string title, string fileName) : base(title, fileName)
        {
         
           
            ValidateAgainstClasses = true;
        }

        public List<UMLOrderedEntity> Entities { get; init; } = new();
        public List<UMLSequenceLifeline> LifeLines { get; init; } = new();

        public bool ValidateAgainstClasses { get; set; }

        public UMLSequenceConnection AddConnection(UMLSequenceLifeline source, UMLSequenceLifeline to, int lineNumber)
        {
            var f = new UMLSequenceConnection(source, to, lineNumber);
            
            Entities.Add(f);

            return f;
        }
    }
}