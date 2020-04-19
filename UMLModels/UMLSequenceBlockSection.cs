using System.Collections.Generic;

namespace UMLModels
{
    public class UMLSequenceBlockSection : UMLOrderedEntity
    {
        public UMLSequenceBlockSection()
        {
        }

        public string Text { get; set; }

        public List<UMLOrderedEntity> Entities { get; set; }

        public UMLSequenceBlockSection(string text, SectionTypes sectionTypes)
        {
            Text = text;
            Entities = new List<UMLOrderedEntity>();
            SectionType = sectionTypes;
        }

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

        public bool IsEnding(string line)
        {
            return line.StartsWith("end");
        }

        public SectionTypes SectionType { get; set; }

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
            IfNoElse
        }

        public bool TakeOverOwnership
        {
            get
            {
                return SectionType == SectionTypes.Else || SectionType == SectionTypes.Catch || SectionType == SectionTypes.Finally;
            }
        }

        public static UMLSequenceBlockSection TryParse(string line)
        {
            if (line.StartsWith("alt"))
                return new UMLSequenceBlockSection(line.Substring(4), UMLSequenceBlockSection.SectionTypes.If);
            else if (line.StartsWith("else"))
                return new UMLSequenceBlockSection(line.Substring(5), SectionTypes.Else);
            else if (line.StartsWith("par"))
                return new UMLSequenceBlockSection(line.Substring(4), SectionTypes.Parrallel);
            else if (line.StartsWith("opt"))
                return new UMLSequenceBlockSection(line.Substring(4), SectionTypes.If);
            else if (line.StartsWith("try"))
                return new UMLSequenceBlockSection(line.Substring(4), SectionTypes.Try);
            else if (line.StartsWith("catch"))
                return new UMLSequenceBlockSection(line.Substring(6), SectionTypes.Catch);
            else if (line.StartsWith("finally"))
                return new UMLSequenceBlockSection(line.Substring(8), SectionTypes.Finally);
            else if (line.StartsWith("break"))
                return new UMLSequenceBlockSection(line.Substring(6), SectionTypes.Break);
            else
                return null;
        }
    }
}