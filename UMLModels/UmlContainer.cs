using System.Collections.Generic;

namespace UMLModels
{
    public class UMLDataType
    {
        public string Namespace { get; set; }

        public UMLDataType()
        {
        }

        public UMLDataType(string name, string @namespace = "", params UMLInterface[] interfaces)
        {
            Namespace = @namespace;
            Name = name;
            Properties = new List<UMLProperty>();
            Methods = new List<UMLMethod>();

            this.Interfaces = new List<UMLInterface>();
            if (interfaces != null && interfaces.Length == 0)
                this.Interfaces.AddRange(interfaces);
        }

        public string Id
        {
            get
            {
                return Name;
            }
        }

        public UMLDataType Base { get; set; }
        public string Name { get; set; }
        public List<UMLProperty> Properties { get; set; }
        public List<UMLMethod> Methods { get; set; }

        public List<UMLInterface> Interfaces { get; set; }
    }

    public class StringDataType : UMLDataType
    {
        public StringDataType() : base("string")
        {
        }
    }

    public class IntDataType : UMLDataType
    {
        public IntDataType() : base("int")
        {
        }
    }

    public class DecimalDataType : UMLDataType
    {
        public DecimalDataType() : base("decimal")
        {
        }
    }

    public class BoolDataType : UMLDataType
    {
        public BoolDataType() : base("bool")
        {
        }
    }

    public class VoidDataType : UMLDataType
    {
        public VoidDataType() : base("void")
        {
        }
    }
}