using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public enum Overridability
    {
        None,
        Virtual,
        Abstract
    }

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

        public bool IsConstructor { get; set; }

        public bool IsStatic { get; set; }

        public string Name { get; set; }

        public Overridability OverridableType { get; set; }

        public List<UMLParameter> Parameters { get; }

        public UMLDataType ReturnType { get; set; }

        public UMLVisibility Visibility
        {
            get; set;
        }

        public override string ToString()
        {
            string vs = Visibility == UMLVisibility.Public ? "+" : Visibility == UMLVisibility.Protected ? "#" : "-";

            StringBuilder sb = new StringBuilder();
            if (ReturnType != null && !string.IsNullOrEmpty(ReturnType.Name))
            {
                sb.Append(ReturnType.Name);
                sb.Append(" ");
            }
            sb.Append(Name);
            sb.Append("(");

            if (Parameters != null)
                for (var x = 0; x < Parameters.Count; x++)
                {
                    sb.Append(Parameters[x].ToString());
                    if (x < Parameters.Count - 1)
                        sb.Append(", ");
                }

            sb.Append(")");

            return sb.ToString();
        }
    }
}