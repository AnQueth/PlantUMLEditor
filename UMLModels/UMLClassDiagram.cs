using System.Collections.Generic;

namespace UMLModels
{
    public class UMLClassDiagram : UMLDiagram
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

      

        public List<UMLDataType> DataTypes { get; set; }
   
    }
}