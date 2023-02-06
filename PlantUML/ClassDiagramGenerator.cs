using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using UMLModels;

namespace PlantUML
{
    internal class ClassDiagramGenerator
    {
        public static void Create(UMLClassDiagram classDiagram, TextWriter writer)
        {
            writer.WriteLine("@startuml");

            writer.Write("title ");
            writer.WriteLine(classDiagram.Title);
            StringBuilder postWriter = new();

            Write(classDiagram.Package.Children, writer, postWriter, classDiagram.DataTypes);
            writer.Write(postWriter.ToString());
            writer.WriteLine();

            foreach (var nc in classDiagram.NoteConnections)
            {

                writer.Write(nc.First);
                writer.Write(' ');
                writer.Write(nc.Connector);
                writer.Write(' ');
                writer.WriteLine(nc.Second);
            }
            writer.WriteLine("@enduml");
        }
        private static void Write(List<UMLDataType> children, TextWriter writer, StringBuilder postWrites,
            List<UMLDataType> dataTypes)
        {
            foreach (UMLDataType? item in children)
            {
                if (item is UMLPackage pa)
                {
                    writer.Write("package ");
                    writer.Write(pa.Text);
                    writer.WriteLine("{");

                    Write(pa.Children, writer, postWrites, dataTypes);

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
                    {
                        writer.Write("interface ");
                    }
                    else if (item is UMLEnum)
                    {
                        writer.Write("enum ");
                    }
                    else if (item is UMLClass cl)
                    {
                        if (cl.IsAbstract)
                        {
                            writer.Write("abstract ");
                        }

                        writer.Write("class ");


                    }

                    if (item.Name.Contains(" ") || item.Name.Contains("?") || !string.IsNullOrWhiteSpace(item.Alias))
                    {
                        writer.Write("\"");
                    }

                    writer.Write(item.Name);
                    if (item.Name.Contains(" ") || item.Name.Contains("?") || !string.IsNullOrWhiteSpace(item.Alias))
                    {
                        writer.Write("\"");
                    }
                    if (item is UMLClass cl2)
                    {
                        if (!string.IsNullOrEmpty(cl2.StereoType))
                        {
                            writer.Write(" <<");
                            writer.Write(cl2.StereoType);
                            writer.Write(">> ");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(item.Alias))
                    {
                        writer.Write(" as ");
                        writer.Write(item.Alias);
                    }

                    writer.WriteLine(" { ");

                    foreach (UMLProperty? prop in item.Properties.Where(z => !z.DrawnWithLine))
                    {


                        writer.Write(GetVisibility(prop.Visibility));
                        if (prop.IsStatic)
                        {
                            writer.Write(" {static}");
                        }

                        if (prop.IsAbstract)
                        {
                            writer.Write(" {abstract}");
                        }

                        writer.Write(" ");



                        if (prop.ListType == ListTypes.None)
                        {
                            writer.Write(prop.ObjectType.Name);
                        }
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

                    foreach (UMLMethod? me in item.Methods)
                    {
                        writer.Write(GetVisibility(me.Visibility));
                        if (me.IsStatic)
                        {
                            writer.Write(" {static}");
                        }

                        if (me.IsAbstract)
                        {
                            writer.Write(" {abstract}");
                        }

                        writer.Write(' ');
                        writer.Write(me.ReturnType.Name);
                        writer.Write(' ');
                        writer.Write(me.Name);
                        writer.Write('(');

                        for (int x = 0; x < me.Parameters.Count; x++)
                        {
                            UMLParameter? p = me.Parameters[x];

                            if (p.ListType == ListTypes.None)
                            {
                                writer.Write(p.ObjectType.Name);
                            }
                            else if (p.ListType == ListTypes.Array)
                            {
                                writer.Write(p.ObjectType.Name);
                                writer.Write("[]");
                            }
                            else if (p.ListType == ListTypes.IReadOnlyCollection)
                            {
                                writer.Write("IReadOnlyCollection<");
                                writer.Write(p.ObjectType.Name);
                                writer.Write('>');
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
                            {
                                writer.Write(", ");
                            }
                        }
                        writer.WriteLine(")");
                    }
                    writer.WriteLine(" } ");
                    if (item is UMLClass c)
                    {
                        foreach (UMLDataType? b in item.Bases)
                        {
                            _ = postWrites.Append(OrAlias(item.NonGenericName, item.Alias));
                            _ = postWrites.Append(" -- ");
                            _ = postWrites.AppendLine(OrAlias(b.NonGenericName, b.Alias));
                        }
                    }

                    foreach (UMLInterface? i in item.Interfaces)
                    {
                        _ = postWrites.Append(OrAlias(item.NonGenericName, item.Alias));
                        _ = postWrites.Append(" --* ");
                        _ = postWrites.AppendLine(OrAlias(i.NonGenericName, i.Alias));
                    }
                    foreach (UMLProperty? prop in item.Properties.Where(z => z.DrawnWithLine && dataTypes.Any(p => p == z.ObjectType)))
                    {


                        postWrites.Append(item.NonGenericName);
                        if (prop.ListType != ListTypes.None)
                        {
                            postWrites.Append(" \"1\" --* \"*\" ");
                        }
                        else
                        {
                            postWrites.Append(" --* ");
                        }

                        postWrites.Append(prop.ObjectType.NonGenericName);
                        if (!string.IsNullOrEmpty(prop.Name))
                        {

                            postWrites.Append(" : ");
                            postWrites.AppendLine(prop.Name);
                        }
                        else
                        {
                            postWrites.AppendLine();
                        }

                    }
                }
            }


        }

        private static string OrAlias(string nonGenericName, string? alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return nonGenericName;


            return alias;
        }

        private static char GetVisibility(UMLVisibility vis)
        {
            return vis switch
            {
                UMLVisibility.Private => '-',
                UMLVisibility.Protected => '#',
                UMLVisibility.Public => '+',
                UMLVisibility.Internal => '~',
                _ => ' ',
            };
        }
    }
}
