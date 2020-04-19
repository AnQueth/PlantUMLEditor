using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public   class UMLSequenceBlock : UMLOrderedEntity
    {
        public UMLSequenceBlock()
        {
            Sections = new List<UMLSequenceBlockSection>();
        }
        public UMLSequenceBlock(params UMLSequenceBlockSection[] sections)
        {
            Sections = new List<UMLSequenceBlockSection>();
            Sections.AddRange(sections);
        }

  
        public virtual List<UMLSequenceBlockSection> Sections { get; set; }

        public UMLSequenceBlockSection Add(string text, UMLSequenceBlockSection.SectionTypes sectionTypes)
        {
            var f = new UMLSequenceBlockSection(text, sectionTypes);

            Sections.Add(f);
            return f;
        }

  

 
    }
}
