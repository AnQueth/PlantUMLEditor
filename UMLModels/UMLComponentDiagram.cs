using System.Collections.Generic;

namespace UMLModels
{
    public class UMLComponentDiagram : UMLDiagram
    {
        public UMLComponentDiagram(string title, string fileName, UMLPackage? package) : base(title, fileName)
        {
            Package = package;

        }

        public List<UMLDataType> Entities
        {
            get
            {
                List<UMLDataType> dt = new();
                if (Package != null)
                {
                    AddMore(Package, dt);
                }

                return dt;
            }
        }

        public List<(string Line, int LineNumber, string message)> ExplainedErrors { get; } = new();
        public UMLPackage? Package
        {
            get; init;
        }

        public List<UMLPackage> ContainedPackages { get; } = new List<UMLPackage>();

        private void AddMore(UMLPackage p, List<UMLDataType> dt)
        {
            foreach (var c in p.Children)
            {
                if (c is UMLPackage z)
                {
                    AddMore(z, dt);
                }
                else if (!(c is UMLNote))
                {
                    dt.Add(c);
                }
            }
        }
    }
}