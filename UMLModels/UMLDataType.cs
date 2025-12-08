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

        public UMLDataType(string name, string @namespace = "", string? alias = null, params UMLInterface[] interfaces)
        {
            Namespace = @namespace;
            Name = name;
            Alias = alias;

            int ix = name.IndexOf('<');
            if (ix > 0)
            {
                NonGenericName = name[..ix];
            }
            else
            {
                NonGenericName = name;
            }

            if (interfaces != null && interfaces.Length > 0)
            {
                Interfaces.AddRange(interfaces);
            }
        }



        public List<UMLDataType> Bases { get; } = new();

        public string Id => Name;

        public List<UMLInterface> Interfaces { get; } = new();
        public int LineNumber
        {
            get; set;
        }
        public List<UMLMethod> Methods { get; } = new();
        public string Name
        {
            get; set;
        }
        public string? Alias
        {
            get; set;
        }
        public string Namespace
        {
            get; set;
        }
        public List<UMLProperty> Properties { get; } = new();

        public string NonGenericName
        {
            get; set;
        }
    }

    public class VoidDataType : UMLDataType
    {
        public VoidDataType() : base("void")
        {
        }
    }
}