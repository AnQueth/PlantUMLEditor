using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLComponent : UMLDataType
    {
        public UMLComponent(string @namespace, string name) : base(name, @namespace)
        {
            Exposes = new List<UMLInterface>();
            Consumes = new List<UMLInterface>();
        }

        public List<UMLInterface> Consumes { get; set; }
        public List<UMLInterface> Exposes { get; set; }
    }
}