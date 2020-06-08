using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UMLModels
{
    public class UMLComponentDiagram : UMLDiagram
    {
        public UMLComponentDiagram(string title, string fileName)
        {
            Title = title;

            FileName = fileName;
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