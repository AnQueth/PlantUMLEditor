using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class DocumentMessageGenerator
    {
        private IEnumerable<UMLDiagram> documents;
        private ObservableCollection<DocumentMessage> messages;

        public DocumentMessageGenerator(IEnumerable<UMLDiagram> documents, ObservableCollection<DocumentMessage> messages)
        {
            this.documents = documents;
            this.messages = messages;
        }

        public void Generate()
        {
            List<DocumentMessage> newMessages = new List<DocumentMessage>();
            List<(UMLDataType, string)> dataTypes = new List<(UMLDataType, string)>();

            foreach (var doc in this.documents)
            {
                if (doc is UMLComponentDiagram f)
                {
                    foreach (var e in f.Errors)
                    {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = f.FileName,
                            Text = e.Line,
                            LineNumber = e.LineNumber,

                            Warning = false
                        });
                    }
                }
                if (doc is UMLClassDiagram f2)
                {
                    foreach (var fdt in f2.DataTypes)
                    {
                        dataTypes.Add((fdt, f2.FileName));
                    }

                    foreach (var e in f2.Errors)
                    {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = f2.FileName,
                            Text = e.Value,
                            LineNumber = e.LineNumber,

                            Warning = false
                        });
                    }
                }
                if (doc is UMLSequenceDiagram o)
                {
                    var items = from z in o.LifeLines
                                where z.Warning != null
                                select new { o = doc, f = z };

                    foreach (var i in items)
                    {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = i.o.FileName,
                            Text = i.f.Warning,
                            LineNumber = i.f.LineNumber,

                            Warning = true
                        });
                    }

                    CheckEntities(o.FileName, o.Entities, o);
                }
            }


            foreach (var dt in dataTypes)
            {
                if (dt.Item1 is UMLEnum)
                    continue;
                foreach (var m in dt.Item1.Properties)
                {
                    string r = GetCleanName(m.ObjectType.Name);
                    var pdt = dataTypes.Any(z => z.Item1.Name == r);
                    if (!pdt)
                    {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = dt.Item2,
                            LineNumber = dt.Item1.LineNumber,
                            Text = r,
                            IsMissingDataType = true
                        });
                    }




                }
                foreach (var m in dt.Item1.Methods)
                {
                    foreach (var p in m.Parameters)
                    {
                        string r = GetCleanName(p.ObjectType.Name);
                        var pdt2 = dataTypes.Any(z => z.Item1.Name == r);
                        if (!pdt2)
                        {
                            newMessages.Add(new DocumentMessage()
                            {
                                FileName = dt.Item2,
                                LineNumber = dt.Item1.LineNumber,
                                Text = r,
                                IsMissingDataType = true
                            });
                        }



                    }
                    if (!string.IsNullOrWhiteSpace(m.ReturnType.Name))
                    {
                        string r = GetCleanName(m.ReturnType.Name);


                        var pdt = dataTypes.Any(z => z.Item1.Name == r);
                        if (!pdt)
                        {
                            newMessages.Add(new DocumentMessage()
                            {
                                FileName = dt.Item2,
                                LineNumber = dt.Item1.LineNumber,
                                Text = r,
                                IsMissingDataType = true

                            });
                        }
                    }



                }
            }


            List<DocumentMessage> removals = new List<DocumentMessage>();
            foreach (var item in messages)
            {
                DocumentMessage m;
                if ((m = newMessages.FirstOrDefault(z => z.FileName == item.FileName && z.Text == item.Text && z.LineNumber == item.LineNumber)) == null)
                {
                    removals.Add(item);
                }
            }

            removals.ForEach(p => messages.Remove(p));

            foreach (var item in newMessages)
            {
                DocumentMessage m;
                if ((m = messages.FirstOrDefault(z => z.FileName == item.FileName && z.Text == item.Text && z.LineNumber == item.LineNumber)) == null)
                {
                    messages.Add(item);
                }
            }

            void CheckEntities(string fileName, List<UMLOrderedEntity> entities, UMLSequenceDiagram o)
            {
                foreach (var g in entities)
                {
                    if (g.Warning != null && g is UMLSequenceConnection c)
                    {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = fileName,
                            LineNumber = g.LineNumber,
                            Text = g.Warning,
                            MissingMethodText = c.Action?.Signature,
                            MissingMethodDataTypeId = c.To?.DataTypeId,
                            Diagram = o,
                            IsMissingMethod = true,
                            Warning = true
                        });
                    }

                    if (g is UMLSequenceBlockSection s)
                    {
                        CheckEntities(fileName, s.Entities, o);
                    }
                }
            }
        }

        private string GetCleanName(string name)
        {
            string r = name;
            Match m2;
            while ((m2 = Regex.Match(r, "(?:IEnumerable|Task|IReadOnlyCollection)\\<(?<f>.+)\\>|(?<f>.+)\\[\\]")).Success)
            {
                if (m2.Success)
                {
                    r = m2.Groups["f"].Value;
                }
            }
            return r;

        }
    }
}