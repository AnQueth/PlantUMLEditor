using System.Collections.Generic;

namespace UMLModels
{
    public class UMLSequenceBlockSection : UMLOrderedEntity
    {



        public UMLSequenceBlockSection(string text, SectionTypes sectionTypes, int lineNumber) : base(lineNumber)
        {
            Text = text;
            Entities = new List<UMLOrderedEntity>();
            SectionType = sectionTypes;
        }

        public enum SectionTypes
        {
            None,
            If,
            Else,
            Try,
            Catch,
            Finally,
            Parrallel,
            Break,
            IfNoElse,
            Loop,
            Group
        }

        public List<UMLOrderedEntity> Entities
        {
            get; set;
        }
        public SectionTypes SectionType
        {
            get; set;
        }

        public bool TakeOverOwnership => SectionType == SectionTypes.Else || SectionType == SectionTypes.Catch || SectionType == SectionTypes.Finally;

        public string Text
        {
            get; set;
        }



        public UMLSequenceConnection AddConnection(UMLSequenceLifeline source, UMLSequenceLifeline to, int lineNumber)
        {
            var f = new UMLSequenceConnection(source, to, lineNumber);
            Entities.Add(f);

            return f;
        }


    }
}