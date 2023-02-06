using System.Collections.Generic;

namespace UMLModels
{
    public class UMLClass : UMLDataType
    {
        public UMLClass(string stereoType, string @namespace, string? alias, bool isAbstract,
            string name, IEnumerable<UMLDataType> baseClasses, params UMLInterface[] interfaces) :
            base(name, @namespace, alias, interfaces)
        {
            StereoType = stereoType;
            if (baseClasses is not null)
            {
                Bases.AddRange(baseClasses);
            }

            IsAbstract = isAbstract;
        }


        public string StereoType
        {
            get; set;
        }

        public bool IsAbstract
        {
            get; set;
        }
    }
}