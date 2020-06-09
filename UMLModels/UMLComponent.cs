using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLComponent : UMLDataType
    {
        public UMLComponent(string @namespace, string name) : base(name, @namespace)
        {
            Exposes = new List<UMLDataType>();
            Consumes = new List<UMLDataType>();
        }

        public List<UMLDataType> Consumes { get; set; }
        public List<UMLDataType> Exposes { get; set; }
    }
}