using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLComment : UMLDataType
    {
        public UMLComment(string text) : base("UMLComment")
        {
            Text = text;
        }

        public string Text
        {
            get; set;
        }
    }
}