using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLNote : UMLDataType
    {
        public UMLNote(string text) : base("UMLNote")
        {
            Text = text;
        }

        public string Text
        {
            get; set;
        }
    }
}