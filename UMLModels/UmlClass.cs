using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
   public class UMLClass : UMLDataType
    {
     
        public UMLClass(string @namespace, string name,  UMLClass baseClass = null, params UMLInterface[] interfaces) : base(name, @namespace, interfaces)
        {
            this.Base = baseClass;
 

        }


  

    }
}
