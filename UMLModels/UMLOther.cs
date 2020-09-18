using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLOther : UMLDataType
    {
        public UMLOther(string text) : base("UMLOther")
        {
            Text = text;
        }

        public string Text
        {
            get; set;
        }
    }
}