using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UMLModels;

namespace PlantUML
{
    public class PlantUMLGenerator
    {
        private static void Create(UMLClassDiagram classDiagram, TextWriter writer)
        {
            writer.WriteLine("@startuml");

            writer.Write("title ");
            writer.WriteLine(classDiagram.Title);
            StringBuilder postWriter = new StringBuilder();
            Write(classDiagram.Package.Children, writer, postWriter);
            writer.Write(postWriter.ToString());
            writer.WriteLine("@enduml");
        }

        private static void Create(UMLSequenceDiagram sequenceDiagram, TextWriter writer)
        {
            writer.WriteLine("@startuml");

            writer.Write("title ");
            writer.WriteLine(sequenceDiagram.Title);

            foreach (var item in sequenceDiagram.LifeLines)
            {
                writer.Write("participant ");
                writer.Write(item.DataTypeId);
                writer.Write(" as ");
                writer.WriteLine(item.Alias);
            }

            DrawEntity(sequenceDiagram.Entities, writer);

            writer.WriteLine("@enduml");
        }

        private static void DrawConnection(TextWriter writer, UMLSequenceConnection item)
        {
            //if (item.To == null)
            //{
            //    if (item.Action.IsConstructor && !(item.Action is UMLReturnFromMethod))
            //        writer.Write(" <-- ");
            //    else
            //        writer.Write(" <- ");

            //    writer.Write(item.From.Alias);
            //}
            //else
            //{
            //    if (item.From != null)
            //        writer.Write(item.From.Alias);

            //    if (item.Action.IsConstructor && !(item.Action is UMLReturnFromMethod))
            //        writer.Write(" --> ");
            //    else
            //        writer.Write(" -> ");

            //    writer.Write(item.To.Alias);
            //}
            //writer.Write(" : ");

            //if (item.Action is UMLReturnFromMethod)
            //{
            //    writer.Write(" return ");
            //    writer.WriteLine(item.Action.Signature);
            //}
            //else
            //{
            //    writer.Write(item.Action.Signature);

            //}
            //writer.WriteLine();
        }

        private static void DrawConnections(TextWriter writer, List<UMLSequenceConnection> connections)
        {
            foreach (var item in connections)
            {
                DrawConnection(writer, item);
            }
        }

        private static void DrawEntity(List<UMLOrderedEntity> entities, TextWriter writer)
        {
            foreach (var entity in entities)
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

                    if (block.SectionType != UMLSequenceBlockSection.SectionTypes.None &&
                        block.SectionType != UMLSequenceBlockSection.SectionTypes.If)
                    {
                        writer.WriteLine("end");
                    }
                }
                else if (entity is UMLSequenceConnection con)
                {
                    DrawConnection(writer, con);
                }
            }
        }

        private static void Write(List<UMLDataType> children, TextWriter writer, StringBuilder postWrites)
        {
            foreach (var item in children)
            {
                if (item is UMLPackage pa)
                {
                    writer.Write("package ");
                    writer.Write(pa.Text);
                    writer.WriteLine("{");

                    Write(pa.Children, writer, postWrites);

                    writer.WriteLine("}");
                }
                else if (item is UMLOther o)
                {
                    writer.WriteLine(o.Text);
                }
                else if (item is UMLComment com)
                {
                    writer.WriteLine(com.Text);
                }
                else if (item is UMLNote note)
                {
                    writer.WriteLine(note.Text);
                }
                else
                {
                    if (item is UMLInterface)
                        writer.Write("interface ");
                    else if (item is UMLEnum)
                        writer.Write("enum ");
                    else
                        writer.Write("class ");

                    if (item.Name.Contains("<") || item.Name.Contains(" ") || item.Name.Contains("?"))

                        writer.Write("\"");
                    writer.Write(item.Name);
                    if (item.Name.Contains("<") || item.Name.Contains(" ") || item.Name.Contains("?"))
                        writer.Write("\"");
                    writer.WriteLine(" { ");

                    foreach (var prop in item.Properties)
                    {
                        writer.Write(prop.Visibility == UMLVisibility.Private ? "-" : prop.Visibility == UMLVisibility.Protected ? "#" : prop.Visibility == UMLVisibility.Public ? "+" : "");
                        writer.Write(" ");

                        if (prop.ListType == ListTypes.None)
                            writer.Write(prop.ObjectType.Name);
                        else if (prop.ListType == ListTypes.Array)
                        {
                            writer.Write(prop.ObjectType.Name);
                            writer.Write("[]");
                        }
                        else if (prop.ListType == ListTypes.IReadOnlyCollection)
                        {
                            writer.Write("IReadOnlyCollection<");
                            writer.Write(prop.ObjectType.Name);
                            writer.Write(">");
                        }
                        else if (prop.ListType == ListTypes.List)
                        {
                            writer.Write("List<");
                            writer.Write(prop.ObjectType.Name);
                            writer.Write(">");
                        }
                        writer.Write(" ");
                        writer.WriteLine(prop.Name);
                    }

                    foreach (var me in item.Methods)
                    {
                        writer.Write(me.ReturnType.Name);
                        writer.Write(" ");
                        writer.Write(me.Name);
                        writer.Write("(");

                        for (var x = 0; x < me.Parameters.Count; x++)
                        {
                            var p = me.Parameters[x];

                            if (p.ListType == ListTypes.None)
                                writer.Write(p.ObjectType.Name);
                            else if (p.ListType == ListTypes.Array)
                            {
                                writer.Write(p.ObjectType.Name);
                                writer.Write("[]");
                            }
                            else if (p.ListType == ListTypes.IReadOnlyCollection)
                            {
                                writer.Write("IReadOnlyCollection<");
                                writer.Write(p.ObjectType.Name);
                                writer.Write(">");
                            }
                            else if (p.ListType == ListTypes.List)
                            {
                                writer.Write("List<");
                                writer.Write(p.ObjectType.Name);
                                writer.Write(">");
                            }
                            writer.Write(" ");
                            writer.Write(p.Name);

                            if (x != me.Parameters.Count - 1)
                                writer.Write(", ");
                        }
                        writer.WriteLine(")");
                    }
                    writer.WriteLine(" } ");
                    if (item is UMLClass c)
                    {
                        foreach (var b in item.Bases)
                        {
                            postWrites.Append(item.Name);
                            postWrites.Append(" --|> ");
                            postWrites.AppendLine(b.Name);
                        }
                    }

                    foreach (var i in item.Interfaces)
                    {
                        postWrites.Append(item.Name);
                        postWrites.Append(" --* ");
                        postWrites.AppendLine(i.Name);
                    }
                    foreach (var prop in item.Properties.Where(z => children.Any(p => p == z.ObjectType)))
                    {
                        writer.Write(item.Name);
                        if (prop.ListType != ListTypes.None)
                        {
                            writer.Write(" \"1\" --* \"*\" ");
                        }
                        else
                            writer.Write(" --* ");

                        writer.Write(prop.ObjectType.Name);
                        writer.Write(" : ");
                        writer.WriteLine(prop.Name);
                    }
                }
            }
        }

        public static string Create(UMLComponentDiagram diagram)
        {
            throw new NotImplementedException();
        }

        public static string Create(UMLClassDiagram classDiagram)
        {
            StringBuilder sb = new StringBuilder();

            using (TextWriter tw = new StringWriter(sb))
            {
                Create(classDiagram, tw);
            }

            return sb.ToString();
        }

        public static string Create(UMLSequenceDiagram sequenceDiagram)
        {
            StringBuilder sb = new StringBuilder();

            using (TextWriter tw = new StringWriter(sb))
            {
                Create(sequenceDiagram, tw);
            }

            return sb.ToString();
        }
    }
}