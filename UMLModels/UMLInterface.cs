using System.Collections.Generic;

namespace UMLModels
{
    public class UMLInterface : UMLDataType
    {


        public UMLInterface(string @namespace, string name, string? alias, IEnumerable<UMLDataType> @bases) : base(name, @namespace, alias)
        {
            Bases.AddRange(@bases);
        }
    }
}