using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace UMLModels
{
    public class UMLSequenceBlockSection : UMLOrderedEntity
    {
        private static readonly Regex _blockSection = new("(?<type>alt|loop|else|par|opt|try|group|catch|finally|break)(?<text>.+)");

       
    
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
            Loop
        }

        public List<UMLOrderedEntity> Entities { get; set; }
        public SectionTypes SectionType { get; set; }

        public bool TakeOverOwnership
        {
            get
            {
                return SectionType == SectionTypes.Else || SectionType == SectionTypes.Catch || SectionType == SectionTypes.Finally;
            }
        }

        public string Text { get; set; }

        public static bool TryParse(string line, int lineNumber,  [NotNullWhen(true)] out UMLSequenceBlockSection? block)
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
                        block =  new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.If, lineNumber);
                        return true;

                    case "else":
                        block = new UMLSequenceBlockSection(text, SectionTypes.Else, lineNumber);
                        return true;

                    case "par":
                        block = new UMLSequenceBlockSection(text, SectionTypes.Parrallel, lineNumber);
                        return true;

                    case "try":
                        block = new UMLSequenceBlockSection(text, SectionTypes.Try, lineNumber);
                        return true;

                    case "catch":
                        block = new UMLSequenceBlockSection(text, SectionTypes.Catch, lineNumber);
                        return true;

                    case "finally":
                        block = new UMLSequenceBlockSection(text, SectionTypes.Finally, lineNumber);
                        return true;

                    case "break":
                        block = new UMLSequenceBlockSection(text, SectionTypes.Break, lineNumber);
                        return true;

                    case "loop":
                        block = new UMLSequenceBlockSection(text, SectionTypes.Loop, lineNumber);
                        return true;
                }
            }
            block = null;
            return false;
        }

        public UMLSequenceConnection AddConnection(UMLSequenceLifeline source, UMLSequenceLifeline to, int lineNumber)
        {
            var f = new UMLSequenceConnection(source, to, lineNumber);
            Entities.Add(f);

            return f;
        }

     
    }
}