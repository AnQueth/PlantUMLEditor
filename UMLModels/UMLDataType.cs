using System.Collections.Generic;

namespace UMLModels
{
    public class BoolDataType : UMLDataType
    {
        public BoolDataType() : base("bool")
        {
        }
    }

    public class DecimalDataType : UMLDataType
    {
        public DecimalDataType() : base("decimal")
        {
        }
    }

    public class IntDataType : UMLDataType
    {
        public IntDataType() : base("int")
        {
        }
    }

    public class StringDataType : UMLDataType
    {
        public StringDataType() : base("string")
        {
        }
    }

    public class UMLDataType
    {
        public UMLDataType()
        {
        }

        public UMLDataType(string name, string @namespace = "", params UMLInterface[] interfaces)
        {
            Namespace = @namespace;
            Name = name;
            Properties = new List<UMLProperty>();
            Methods = new List<UMLMethod>();
            Bases = new List<UMLDataType>();

            Interfaces = new List<UMLInterface>();
            if (interfaces != null && interfaces.Length == 0)
                Interfaces.AddRange(interfaces);
        }

        public List<UMLDataType> Bases { get; set; }

        public string Id
        {
            get
            {
                return Name;
            }
        }

        public List<UMLInterface> Interfaces { get; set; }
        public int LineNumber { get; set; }
        public List<UMLMethod> Methods { get; set; }
        public string Name { get; set; }
        public string Namespace { get; set; }
        public List<UMLProperty> Properties { get; set; }
    }

    public class VoidDataType : UMLDataType
    {
        public VoidDataType() : base("void")
        {
        }
    }
}