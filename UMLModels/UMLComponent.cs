using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLComponent : UMLDataType
    {
        public UMLComponent(string @namespace, string name, string alias) : base(name, @namespace)
        {
            Exposes = new List<UMLDataType>();
            Consumes = new List<UMLDataType>();
            Alias = alias;
        }

        public List<UMLDataType> Consumes { get; set; }
        public string Alias { get; }
        public List<UMLDataType> Exposes { get; set; }
    }
}