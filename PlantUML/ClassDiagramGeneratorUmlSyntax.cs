using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UMLModels;

namespace PlantUML
{
    /// <summary>
    /// Generator that emits properties and methods using canonical UML syntax:
    /// - Property: visibility name : Type [multiplicity] = default {modifiers}
    /// - Method:   visibility name(param : Type, ...) : ReturnType {modifiers}
    /// </summary>
    public static class ClassDiagramGeneratorUmlSyntax
    {
        public static void Create(UMLClassDiagram classDiagram, TextWriter writer)
        {
            writer.WriteLine("@startuml");

            writer.Write("title ");
            writer.WriteLine(classDiagram.Title);
            var postWriter = StringBuilderPool.Rent();

            Write(classDiagram.Package.Children, writer, postWriter, classDiagram.DataTypes);
            writer.Write(postWriter.ToString());
            StringBuilderPool.Return(postWriter);
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
                    writer.WriteLine(" {");

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

                    if (item.Name.Contains(" ") || item.Name.Contains("?") || item.Name.Contains("<"))
                    {
                        writer.Write("\"");
                    }

                    writer.Write(item.Name);

                    if (item.Name.Contains(" ") || item.Name.Contains("?") || item.Name.Contains("<"))
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

                    writer.WriteLine(" {");

                    // Properties: emit as "visibility name : Type [multiplicity] = default {modifiers}"
                    foreach (UMLProperty? prop in item.Properties.Where(z => !z.DrawnWithLine))
                    {
                        Write(writer, prop);

                        writer.WriteLine();
                    }

                    // Methods: emit as "visibility name(param : Type, ...) : ReturnType {modifiers}"
                    foreach (UMLMethod? me in item.Methods)
                    {
                        Write(writer, me);

                        writer.WriteLine();
                    }

                    writer.WriteLine(" }");

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

        public static void Write(TextWriter writer, UMLMethod me)
        {
            writer.Write(GetVisibility(me.Visibility));
            writer.Write(' ');

            writer.Write(me.Name);
            writer.Write('(');

            for (int x = 0; x < me.Parameters.Count; x++)
            {
                UMLParameter? p = me.Parameters[x];

                // parameter syntax: name : Type [multiplicity]
                writer.Write(p.Name);
                writer.Write(": ");
                WriteTypeWithMultiplicity(writer, p.ObjectType.Name, p.ListType);

                if (x != me.Parameters.Count - 1)
                {
                    writer.Write(", ");
                }
            }

            writer.Write("): ");
            writer.Write(me.ReturnType.Name);

            var mmods = GetModifiers(me.IsStatic, me.IsAbstract);
            if (!string.IsNullOrEmpty(mmods))
            {
                writer.Write(' ');
                writer.Write('{');
                writer.Write(mmods);
                writer.Write('}');
            }
        }

        public static void Write(TextWriter writer, UMLProperty prop)
        {

            // visibility
            writer.Write(GetVisibility(prop.Visibility));
            writer.Write(' ');

            // name
            writer.Write(prop.Name);

            writer.Write(": ");

            // type + multiplicity
            WriteTypeWithMultiplicity(writer, prop.ObjectType.Name, prop.ListType);

            // default value
            if (!string.IsNullOrWhiteSpace(prop.DefaultValue))
            {
                writer.Write(" = ");
                writer.Write(prop.DefaultValue);
            }

            // modifiers
            var modifiers = GetModifiers(prop.IsStatic, prop.IsAbstract);
            if (!string.IsNullOrEmpty(modifiers))
            {
                writer.Write(' ');
                writer.Write('{');
                writer.Write(modifiers);
                writer.Write('}');
            }
        }

        private static void WriteTypeWithMultiplicity(TextWriter writer, string typeName, ListTypes listType)
        {
            switch (listType)
            {
                case ListTypes.None:
                    writer.Write(typeName);
                    break;
                case ListTypes.Array:
                case ListTypes.List:
                case ListTypes.IReadOnlyCollection:
                    // use UML multiplicity for collections
                    writer.Write(typeName);
                    writer.Write("[*]");
                    break;
                default:
                    writer.Write(typeName);
                    break;
            }
        }

        private static string GetModifiers(bool isStatic, bool isAbstract)
        {
            var mods = new List<string>();
            if (isStatic) mods.Add("static");
            if (isAbstract) mods.Add("abstract");
            return string.Join(",", mods);
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
