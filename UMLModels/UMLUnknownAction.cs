using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
   public class UMLUnknownAction : UMLMethod
     {
        public UMLUnknownAction()
        {

        }

        public UMLUnknownAction(string text) : this(text, new VoidDataType()) 
        {

        }

        public UMLUnknownAction(string name, UMLDataType type, params UMLParameter[] parameters) : base(name, type, parameters)
        {
        }

        public UMLUnknownAction(UMLDataType type, params UMLParameter[] parameters) : base(type, parameters)
        {
        }
    }
}
