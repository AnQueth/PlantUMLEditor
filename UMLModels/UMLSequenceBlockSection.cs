using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLSequenceBlockSection
    {
        public UMLSequenceBlockSection()
        {

        }
        public string Text { get; set; }

        public List<UMLSequenceConnection> Connections { get; set; }


        public UMLSequenceBlockSection(string text, SectionTypes sectionTypes)
        {
            Text = text;
            Connections = new List<UMLSequenceConnection>();
            SectionType = sectionTypes;

        }


        public UMLSequenceConnection AddConnection(UMLSequenceLifeline source, UMLSequenceLifeline to)
        {
            var f = new UMLSequenceConnection()
            {
                From = source,
                To = to

            };
            Connections.Add(f);


            return f;

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
    }
}
