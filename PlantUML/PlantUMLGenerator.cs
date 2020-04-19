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

        public string Create(UMLClassDiagram classDiagram)
        {
            StringBuilder sb = new StringBuilder();

            using (TextWriter tw = new StringWriter(sb))
            {

                Create(classDiagram, tw);
            }

            return sb.ToString();

        }
        private void Create(UMLClassDiagram classDiagram, TextWriter writer)
        {
            writer.WriteLine("@startuml");

            writer.Write("title ");
            writer.WriteLine(classDiagram.Title);

            foreach (var n in classDiagram.DataTypes.GroupBy(p => p.Namespace))
            {
                if (!string.IsNullOrEmpty(n.Key))
                {
                    writer.Write(" package ");
                    writer.Write(n.Key);
                    writer.WriteLine("{ ");
                }
                foreach (var item in n)
                {


                    if (item is UMLInterface)
                        writer.Write("interface ");
                    else
                        writer.Write("class ");

                    writer.Write(item.Name);
                    writer.WriteLine(" { ");


                    foreach (var prop in item.Properties)
                    {

                        writer.Write(prop.ObjectType.Name);
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
                            else if(p.ListType == ListTypes.IReadOnlyCollection)
                            {
                                writer.Write("IReadOnlyCollection<");
                                writer.Write(p.ObjectType.Name);
                                writer.Write(">");
                            }
                            else if(p.ListType == ListTypes.List)
                            {
                                writer.Write("List<");
                                writer.Write(p.ObjectType.Name);
                                writer.Write(">");
                            }
                            writer.Write(" ");
                            writer.Write(p.Name);

                            if (x != me.Parameters.Count - 1)
                                writer.Write(",");

                        }
                        writer.WriteLine(")");

                    }
                    writer.WriteLine(" } ");
                }
                if (!string.IsNullOrEmpty(n.Key))
                {
                    writer.WriteLine(" } ");
                }
            }


            foreach (var n in classDiagram.DataTypes.GroupBy(p => p.Namespace))
            {
                foreach (var item in n)
                {
                    if (item is UMLClass c)
                    {
                        if (c.Base != null)
                        {
                            writer.Write(item.Name);
                            writer.Write(" -- ");
                            writer.WriteLine(c.Base.Name);
                        }
                    }

                    foreach (var i in item.Interfaces)
                    {
                        writer.Write(item.Name);
                        writer.Write(" -- ");
                        writer.WriteLine(i.Name);
                    }
                    foreach (var prop in item.Properties)
                    {

                        writer.Write(item.Name);
                        if (prop.ListType != ListTypes.None)
                        {
                            writer.Write(" \"1\" --o \"*\" ");
                        }
                        else
                            writer.Write(" --o ");

                        writer.Write(prop.ObjectType.Name);
                        writer.Write(" : ");
                        writer.WriteLine(prop.Name);
                    }
                }
            }

            writer.WriteLine("@enduml");
        }

        public string Create(UMLSequenceDiagram sequenceDiagram)
        {
            StringBuilder sb = new StringBuilder();

            using (TextWriter tw = new StringWriter(sb))
            {

                Create(sequenceDiagram, tw);
            }

            return sb.ToString();

        }

        private void Create(UMLSequenceDiagram sequenceDiagram, TextWriter writer)
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



            foreach (var entity in sequenceDiagram.Entities)
            {
                if (entity is UMLSequenceBlock block)
                {
                    foreach (var sec in block.Sections)
                    {
                        if (sec.SectionType == UMLSequenceBlockSection.SectionTypes.If)
                        {

                            writer.Write("alt ");
                            writer.WriteLine(sec.Text);

                        }
                        else if (sec.SectionType == UMLSequenceBlockSection.SectionTypes.Else)
                        {
                            writer.Write("else ");
                            writer.WriteLine(sec.Text);
                        }


                        else if (sec.SectionType == UMLSequenceBlockSection.SectionTypes.Parrallel)
                        {
                            writer.Write("par ");
                            writer.WriteLine(sec.Text);

                        }


                        DrawConnections(writer, sec.Connections );

                    



                        if (sec.SectionType != UMLSequenceBlockSection.SectionTypes.None &&
                            sec.SectionType != UMLSequenceBlockSection.SectionTypes.If)
                        {
                            writer.WriteLine("end");
                        }
                    }
                }
                else if(entity is UMLSequenceConnection con)
                {

                    DrawConnection(writer, con);

                }
            }

            writer.WriteLine("@enduml");
        }

        private void DrawConnections(TextWriter writer, List<UMLSequenceConnection> connections )
        {
            foreach (var item in connections)
            {
                DrawConnection(writer, item);
            }
        }

        private static void DrawConnection(TextWriter writer,UMLSequenceConnection item )
        {
            if (item.To == null)
            {

                if (item.Action.IsConstructor && !(item.Action is UMLReturnFromMethod))
                    writer.Write(" <-- ");
                else
                    writer.Write(" <- ");

                writer.Write(item.From.Alias);
            }
            else
            {

                if (item.From != null)
                    writer.Write(item.From.Alias);

                if (item.Action.IsConstructor && !(item.Action is UMLReturnFromMethod))
                    writer.Write(" --> ");
                else
                    writer.Write(" -> ");

                writer.Write(item.To.Alias);
            }
            writer.Write(" : ");

            if (item.Action is UMLReturnFromMethod)
            {
                writer.Write(" return ");
                writer.WriteLine(item.Action.ReturnType.Name);
            }
            else
            {

                writer.Write(item.Action.ReturnType?.Name);
                writer.Write(" ");
                writer.Write(item.Action.Name);
                writer.Write("(");
                foreach (var p in item.Action.Parameters)
                {
                    writer.Write(p.Name);
                    writer.Write(" ");
                }
                writer.Write(")");
            }
            writer.WriteLine();
        }
    }
}
