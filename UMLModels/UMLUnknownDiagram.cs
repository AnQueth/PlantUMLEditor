using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLUnknownDiagram : UMLDiagram
    {
        public UMLUnknownDiagram()
        {
        }

        public UMLUnknownDiagram(string title, string fileName)
        {
            Title = title;

            FileName = fileName;
        }
    }
}