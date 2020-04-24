using System.Collections.Generic;

namespace UMLModels
{
    public class UMLMethod : UMLSignature
    {
        public UMLMethod()
        {
        }

        public UMLMethod(string name, UMLDataType type, UMLVisibility visibility, params UMLParameter[] parameters)
        {
            Name = name;
            ReturnType = type;
            Parameters = new List<UMLParameter>();
            Parameters.AddRange(parameters);
            Visibility = visibility;
        }

        public UMLMethod(UMLDataType type, UMLVisibility visibility, params UMLParameter[] parameters) : this("constructor", type, visibility, parameters)
        {
            IsConstructor = true;
        }


        public UMLVisibility Visibility
        {
            get; set;
        }

        public bool IsConstructor { get; set; }

        public string Name { get; set; }
        public UMLDataType ReturnType { get; set; }
        public List<UMLParameter> Parameters { get; }

        public Overridability OverridableType { get; set; }


        public bool IsStatic {get;set;}

        public override string ToString()
        {
            string vs = Visibility == UMLVisibility.Public ? "+" : Visibility == UMLVisibility.Protected ? "#" : "-";

            string s = $"{ReturnType.Name} {Name}(";

            if (Parameters != null)
                foreach (var p in Parameters)
                {
                    s += p.ToString();
                }

            s += ")";

            return s;
        }
    }

    public enum Overridability
    {
        None,
        Virtual,
        Abstract
    }
}