using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

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

            string s = $"{ReturnType?.Name} {Name}(";

            if (Parameters != null)
                for (var x  =0; x < Parameters.Count; x++)
                {
                    s += Parameters[x].ToString();
                    if (x < Parameters.Count - 1)
                        s += ",";
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