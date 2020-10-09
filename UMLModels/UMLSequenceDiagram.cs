using System.Collections.Generic;

namespace UMLModels
{
    public class UMLSequenceDiagram : UMLDiagram
    {
        public UMLSequenceDiagram()
        {
        }

        public UMLSequenceDiagram(string title, string fileName)
        {
            Title = title;
            LifeLines = new List<UMLSequenceLifeline>();
            Entities = new List<UMLOrderedEntity>();
            FileName = fileName;
            ValidateAgainstClasses = true;
        }

        public List<UMLOrderedEntity> Entities { get; set; }
        public List<UMLSequenceLifeline> LifeLines { get; set; }

        public bool ValidateAgainstClasses { get; set; }

        public UMLSequenceConnection AddConnection(UMLSequenceLifeline source, UMLSequenceLifeline to)
        {
            var f = new UMLSequenceConnection()
            {
                From = source,
                To = to
            };
            Entities.Add(f);

            return f;
        }
    }
}