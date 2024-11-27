using System.Collections.Generic;

namespace UMLModels
{
    public class UMLStruct : UMLDataType
    {


        public UMLStruct(string @namespace, string name, string? alias, IEnumerable<UMLDataType> @bases) : base(name, @namespace, alias)
        {
            Bases.AddRange(@bases);
        }
    }
}