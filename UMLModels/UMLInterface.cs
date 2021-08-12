using System.Collections.Generic;

namespace UMLModels
{
    public class UMLInterface : UMLDataType
    {
     

        public UMLInterface(string @namespace, string name, IEnumerable<UMLDataType> @bases) : base(name, @namespace)
        {
            Bases.AddRange( @bases);
        }
    }
}