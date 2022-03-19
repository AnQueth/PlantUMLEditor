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

        public UMLMethod(string name, UMLDataType type, UMLVisibility visibility, params UMLParameter[] parameters)
        {
            Name = name;
            ReturnType = type;

            Parameters.AddRange(parameters);
            Visibility = visibility;
        }

        public UMLMethod(UMLDataType type, UMLVisibility visibility, params UMLParameter[] parameters) : this("constructor", type, visibility, parameters)
        {
            IsConstructor = true;
        }

        public bool IsConstructor
        {
            get; init;
        }

        public bool IsStatic
        {
            get; init;
        }

        public string Name
        {
            get; init;
        }

        public Overridability OverridableType
        {
            get; init;
        }

        public List<UMLParameter> Parameters { get; } = new();

        public UMLDataType ReturnType
        {
            get; init;
        }

        public UMLVisibility Visibility
        {
            get; init;
        }

        public override string ToString()
        {
            string vs = Visibility == UMLVisibility.Public ? "+" : Visibility == UMLVisibility.Protected ? "#" : "-";

            StringBuilder sb = new();

            if (ReturnType != null && !string.IsNullOrEmpty(ReturnType.Name))
            {
                sb.Append(ReturnType.Name);
                sb.Append(' ');
            }
            sb.Append(Name);
            sb.Append('(');

            if (Parameters != null)
            {
                for (int x = 0; x < Parameters.Count; x++)
                {
                    sb.Append(Parameters[x].ToString());
                    if (x < Parameters.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }
            }

            sb.Append(')');

            return sb.ToString();
        }
    }
}