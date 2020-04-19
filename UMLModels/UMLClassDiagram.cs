using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
  public   class UMLClassDiagram
    {
        public UMLClassDiagram()
        {

        }
        public UMLClassDiagram(string title, string fileName)
        {
            Title = title;
            DataTypes = new List<UMLDataType>();
            FileName = fileName;
        }
        public string Title { get; set; }


        public List<UMLDataType> DataTypes { get; set; }
        public string FileName { get; set; }
    }
}
