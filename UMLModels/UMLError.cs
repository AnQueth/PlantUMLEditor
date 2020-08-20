using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLError
    {
        public UMLError(string text, string value, int lineNumber)
        {
             
            Value = text + " " +  value;
            LineNumber = lineNumber;
        }

   
        public string Value { get; }
        public int LineNumber { get; }
    }
}
