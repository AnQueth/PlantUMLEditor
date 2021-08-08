using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UMLModels;

namespace PlantUML
{
    public class PlantUMLGenerator
    {
  
       





     
      
        public static string Create(UMLComponentDiagram diagram)
        {
            throw new NotImplementedException();
        }

        public static string Create(UMLClassDiagram classDiagram)
        {
            StringBuilder sb = new();

            using (TextWriter tw = new StringWriter(sb, CultureInfo.InvariantCulture))
            {
                ClassDiagramGenerator.Create(classDiagram, tw);
            }

            return sb.ToString();
        }

        public static string Create(UMLSequenceDiagram sequenceDiagram)
        {

            StringBuilder sb = new();

            using (TextWriter tw = new StringWriter(sb, CultureInfo.InvariantCulture))
            {
                SequenceDiagramGenerator.Create(sequenceDiagram, tw);
            }

            return sb.ToString();
        }
    }
}