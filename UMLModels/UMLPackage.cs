using System.Collections.Generic;

namespace UMLModels
{
    public class UMLPackage : UMLDataType
    {
        public UMLPackage(string text, string? type = null, string? alias = null) : base("UMLPackage")
        {
            Text = text;
            Type = type;
            Alias = alias;
            Children = new();
        }

        public List<UMLDataType> Children
        {
            get; set;
        }

        public string Text
        {
            get; set;
        }

        public string? Type
        {
            get; set;
        }
        public string? Alias
        {
            get;
            set;
        }
    }
}