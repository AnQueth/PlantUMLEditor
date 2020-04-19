using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
   public  class UMLReturnFromMethod : UMLMethod
    {
        public UMLReturnFromMethod()
        {

        }
        public UMLReturnFromMethod(UMLMethod returningFrom) : base(returningFrom.ReturnType)
        {
           

        }

        
    }
}
