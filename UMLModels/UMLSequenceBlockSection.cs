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
            Group,
            Critical,
            Ref
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





    }
}