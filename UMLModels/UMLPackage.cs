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
            Children = new List<UMLDataType>();
        }

        public List<UMLDataType> Children
        {
            get; set;
        }

        public string Text
        {
            get; set;
        }
    }
}