using System.Collections.Generic;
using System.IO;
using UMLModels;

namespace PlantUML
{
    internal static class SequenceDiagramGenerator
    {
        internal static void Create(UMLSequenceDiagram sequenceDiagram, TextWriter writer)
        {
            writer.WriteLine("@startuml");

            writer.Write("title ");
            writer.WriteLine(sequenceDiagram.Title);

            foreach (UMLSequenceLifeline? item in sequenceDiagram.LifeLines)
            {
                writer.Write("participant ");
                writer.Write(item.DataTypeId);
                writer.Write(" as ");
                writer.WriteLine(item.Alias);
            }

            DrawEntity(sequenceDiagram.Entities, writer);

            writer.WriteLine("@enduml");

        }
        private static void DrawEntity(List<UMLOrderedEntity> entities, TextWriter writer)
        {
            foreach (UMLOrderedEntity? entity in entities)
            {
                if (entity is UMLSequenceBlockSection block)
                {
                    if (block.SectionType == UMLSequenceBlockSection.SectionTypes.If)
                    {
                        writer.Write("alt ");
                        writer.WriteLine(block.Text);
                    }
                    else if (block.SectionType == UMLSequenceBlockSection.SectionTypes.Else)
                    {
                        writer.Write("else ");
                        writer.WriteLine(block.Text);
                    }
                    else if (block.SectionType == UMLSequenceBlockSection.SectionTypes.Parrallel)
                    {
                        writer.Write("par ");
                        writer.WriteLine(block.Text);
                    }

                    DrawEntity(block.Entities, writer);

                    if (block.SectionType is not UMLSequenceBlockSection.SectionTypes.None and
                        not UMLSequenceBlockSection.SectionTypes.If)
                    {
                        writer.WriteLine("end");
                    }
                }
                else if (entity is UMLSequenceConnection)
                {

                }
            }
        }

    }
}
