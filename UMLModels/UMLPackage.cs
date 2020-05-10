using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLPackage : UMLDataType
    {
        public UMLPackage(string text) : base("UMLPackage")
        {
            Text = text;
        }

        public string Text
        {
            get; set;
        }
    }
}