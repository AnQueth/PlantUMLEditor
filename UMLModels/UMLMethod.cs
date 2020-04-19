using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UMLModels
{
    public class UMLMethod : UMLSignature
    {

        public UMLMethod()
        {

        }

        public UMLMethod(string name, UMLDataType type, params UMLParameter[] parameters)
        {
            Name = name;
            ReturnType = type;
            Parameters = new List<UMLParameter>();
            Parameters.AddRange(parameters);

        }
        public UMLMethod(UMLDataType type, params UMLParameter[] parameters) : this("constructor", type, parameters)
        {
            IsConstructor = true;


        }

        public bool IsConstructor { get; set; }

        public string Name { get; set; }
        public UMLDataType ReturnType { get; set; }
        public List<UMLParameter> Parameters { get; }

        public Overridability OverridableType { get; set; }

    

        public override string ToString()
        {
            string s = $"{ReturnType.Name} {Name} (";

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
