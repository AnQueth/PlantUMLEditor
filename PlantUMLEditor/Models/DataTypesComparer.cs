using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class DataTypesComparer : IEqualityComparer<Tuple<string, UMLDataType>>
    {
        public bool Equals([AllowNull] Tuple<string, UMLDataType> x, [AllowNull] Tuple<string, UMLDataType> y)
        {
            return x.Item1 == y.Item1 && x.Item2.Name == y.Item2.Name && x.Item2.Namespace == y.Item2.Namespace
                && x.Item2.LineNumber == y.Item2.LineNumber;
        }

        public int GetHashCode([DisallowNull] Tuple<string, UMLDataType> obj)
        {
            string h = obj.Item1 + obj.Item2.Name + obj.Item2.Namespace + obj.Item2.LineNumber;
            return h.GetHashCode();
        }
    }
}