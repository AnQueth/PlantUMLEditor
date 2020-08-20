using System.Collections.Generic;

namespace UMLModels
{
    public class UMLInterface : UMLDataType
    {
        public UMLInterface()
        {
        }

        public UMLInterface(string @namespace, string name, List<UMLDataType> @bases ) : base(name, @namespace)
        {
            Bases = @bases;
        }
    }
}