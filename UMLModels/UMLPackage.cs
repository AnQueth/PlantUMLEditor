using System.Collections.Generic;

namespace UMLModels
{
    public class UMLPackage : UMLDataType
    {
        public UMLPackage(string text, string type = null) : base("UMLPackage")
        {
            Text = text;
            Type = type;
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

        public string Type { get; set; }
    }
}