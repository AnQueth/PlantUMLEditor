using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
   public class UMLProperty : UMLParameter
    {
        public UMLProperty()
        {

        }
        public UMLProperty(string name, UMLDataType type, ListTypes listType ) : base(name, type, listType)
        {
           
            
        }

    

        public bool IsReadOnly { get; set; }
 


    }
}
