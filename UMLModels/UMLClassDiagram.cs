using System.Collections.Generic;

namespace UMLModels
{
    public class UMLClassDiagram : UMLDiagram
    {
        public UMLClassDiagram()
        {
            Errors = new List<UMLError>();
        }

        public UMLClassDiagram(string title, string fileName) : this()
        {
            Title = title;

            FileName = fileName;
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

        public List<UMLError> Errors { get; }
        public UMLPackage Package { get; set; }

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