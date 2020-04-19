using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLSequenceConnection : UMLOrderedEntity
    {
        public UMLSequenceLifeline From { get; set; }
        public UMLSequenceLifeline To { get; set; }
    


 

        public UMLMethod Action { get; set; }


    }
}
