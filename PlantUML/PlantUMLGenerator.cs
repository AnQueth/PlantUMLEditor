using System;
using System.Globalization;
using System.IO;
using System.Text;
using UMLModels;

namespace PlantUML
{
    public static class PlantUMLGenerator
    {









        public static string Create(UMLComponentDiagram diagram)
        {
            throw new NotImplementedException();
        }

        public static string Create(UMLClassDiagram classDiagram, bool pureUMLMode)
        {
            StringBuilder sb = new();

            using (TextWriter tw = new StringWriter(sb, CultureInfo.InvariantCulture))
            {
                if (pureUMLMode)
                {
                    ClassDiagramGeneratorUmlSyntax.Create(classDiagram, tw);
                }
                else
                {
                    ClassDiagramGenerator.Create(classDiagram, tw);
                }
        
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