using System.Collections.Generic;
using System.Text.RegularExpressions;

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
            IfNoElse,
            Loop
        }

        public bool TakeOverOwnership
        {
            get
            {
                return SectionType == SectionTypes.Else || SectionType == SectionTypes.Catch || SectionType == SectionTypes.Finally;
            }
        }

        private static Regex _blockSection = new Regex("(?<type>alt|loop|else|par|opt|try|group|catch|finally|break)(?<text>.+)");

        public static UMLSequenceBlockSection TryParse(string line)
        {
            var blockSection = _blockSection.Match(line);
            if (blockSection.Success)
            {
                var name = blockSection.Groups["type"].Value;
                string text = blockSection.Groups["text"].Value;

                switch (name.ToLowerInvariant())
                {
                    case "opt":
                    case "alt":
                        return new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.If);
                    case "else":
                        return new UMLSequenceBlockSection(text, SectionTypes.Else);
                    case "par":
                        return new UMLSequenceBlockSection(text, SectionTypes.Parrallel);
                    case "try":
                        return new UMLSequenceBlockSection(text, SectionTypes.Try);
                    case "catch":
                        return new UMLSequenceBlockSection(text, SectionTypes.Catch);
                    case "finally":
                        return new UMLSequenceBlockSection(text, SectionTypes.Finally);
                    case "break":
                        return new UMLSequenceBlockSection(text, SectionTypes.Break);
                    case "loop":
                        return new UMLSequenceBlockSection(text, SectionTypes.Loop);
                }

            }
            return null;
        }
    }
}