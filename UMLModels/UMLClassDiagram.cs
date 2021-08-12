using System.Collections.Generic;

namespace UMLModels
{
    public class UMLClassDiagram : UMLDiagram
    {

        private readonly UMLPackage _package;

        public UMLClassDiagram(string title, string fileName, UMLPackage? package = null)  : base(title, fileName)
        {
            _package = package ?? new UMLPackage("defaults");
        }

        public List<UMLDataType> DataTypes
        {
            get
            {
                List<UMLDataType> dt = new();

                AddMore(Package, dt);

                return dt;
            }
        }

        public List<UMLError> Errors { get; } = new();
        public UMLPackage Package { get => _package; }

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