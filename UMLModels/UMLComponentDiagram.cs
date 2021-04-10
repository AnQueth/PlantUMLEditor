using System.Collections.Generic;

namespace UMLModels
{
    public class UMLComponentDiagram : UMLDiagram
    {
        public UMLComponentDiagram(string title, string fileName)
        {
            Title = title;

            FileName = fileName;
            Errors = new List<(string Line, int LineNumber)>();
        }

        public List<UMLDataType> Entities
        {
            get
            {
                List<UMLDataType> dt = new List<UMLDataType>();

                AddMore(Package, dt);

                return dt;
            }
        }

        public List<(string Line, int LineNumber)> Errors { get; set; }
        public UMLPackage Package { get; set; }

        public List<UMLPackage> ContainedPackages { get; set; } = new List<UMLPackage>();

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