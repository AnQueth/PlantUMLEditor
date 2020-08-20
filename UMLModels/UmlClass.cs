using System.Collections.Generic;

namespace UMLModels
{
    public class UMLClass : UMLDataType
    {
        public UMLClass(string @namespace, bool isAbstract, 
            string name, List<UMLDataType> baseClasses, params UMLInterface[] interfaces) : base(name, @namespace, interfaces)
        {
            this.Bases = baseClasses;
            this.IsAbstract = isAbstract;
        }

        public bool IsAbstract { get;   set; }


    }
}