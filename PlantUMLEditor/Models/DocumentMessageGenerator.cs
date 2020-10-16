using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xaml;
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

        private string[] GetCleanName(string name)
        {
            string[] knownwords = {"Task", "List", "IReadOnlyCollection",
                "IList", "IEnumerable", "Dictionary", "out", "var", "HashSet","IEnumerableTask", "IHandler"};

            string[] parts = name.Split(' ', '.', ',', '<', '>', '[', ']');

            List<string> types = new List<string>();
            foreach(var p in parts)
            {
                if (string.IsNullOrEmpty(p) || knownwords.Contains(p))
                    continue;

                types.Add(p);

            }

            return types.ToArray();


         
        }

        public async Task Generate(string folderBase)
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
                            RelativeFileName = f.FileName.Substring(folderBase.Length + 1),
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
                            RelativeFileName = f2.FileName.Substring(folderBase.Length + 1),
                            Warning = false
                        });
                    }
                }
                if (doc is UMLSequenceDiagram o)
                {
                    if (o.ValidateAgainstClasses)
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
                                RelativeFileName = i.o.FileName.Substring(folderBase.Length + 1),
                                Warning = true
                            });
                        }

                        CheckEntities(o.FileName, folderBase, o.Entities, o);

                    }
                }
            }

            List<((string fileName, int lineNumber, string ns1, string dt), string ns2)> namespaceReferences 
                = new List<((string fileName, int lineNumber, string ns1, string dt), string ns2)>();

            foreach (var dt in dataTypes)
            {
                if (dt.Item1 is UMLEnum)
                    continue;
                foreach (var m in dt.Item1.Properties)
                {
                    var parsedTypes = GetCleanName(m.ObjectType.Name);
                    foreach (var r in parsedTypes)
                    {
                        var pdt = dataTypes.FirstOrDefault(z => z.Item1.Name == r);
                        if (pdt == default)
                        {
                            newMessages.Add(new DocumentMessage()
                            {
                                FileName = dt.Item2,
                                RelativeFileName = dt.Item2.Substring(folderBase.Length + 1),
                                LineNumber = dt.Item1.LineNumber,
                                Text = r,
                                IsMissingDataType = true
                            });
                        }
                        else
                        {
                            namespaceReferences.Add(((dt.Item2 , dt.Item1.LineNumber, dt.Item1.Namespace, r), pdt.Item1.Namespace));
                        }
                    }
                }
                foreach (var m in dt.Item1.Methods)
                {
                    foreach (var p in m.Parameters)
                    {
                        var parsedTypes = GetCleanName(p.ObjectType.Name);
                        foreach (var r in parsedTypes)
                        {
                            var pdt2 = dataTypes.FirstOrDefault(z => z.Item1.Name == r);
                            if (pdt2 == default)
                            {
                                newMessages.Add(new DocumentMessage()
                                {
                                    FileName = dt.Item2,
                                    RelativeFileName = dt.Item2.Substring(folderBase.Length + 1),
                                    LineNumber = dt.Item1.LineNumber,
                                    Text = r,
                                    IsMissingDataType = true
                                });
                            }
                            else
                            {
                                namespaceReferences.Add(((dt.Item2, dt.Item1.LineNumber, dt.Item1.Namespace,r), pdt2.Item1.Namespace));

                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(m.ReturnType.Name))
                    {
                        var parsedTypes = GetCleanName(m.ReturnType.Name);
                        foreach (var r in parsedTypes)
                        {
                            var pdt = dataTypes.FirstOrDefault(z => z.Item1.Name == r);
                            if (pdt == default)
                            {
                                newMessages.Add(new DocumentMessage()
                                {
                                    FileName = dt.Item2,
                                    RelativeFileName = dt.Item2.Substring(folderBase.Length + 1),
                                    LineNumber = dt.Item1.LineNumber,
                                    Text = r,
                                    IsMissingDataType = true
                                });
                            }
                            else
                            {
                                namespaceReferences.Add(((dt.Item2, dt.Item1.LineNumber, dt.Item1.Namespace,r), pdt.Item1.Namespace));

                            }
                        }
                    }
                }
            }

            foreach(var n in namespaceReferences)
            {
                if(namespaceReferences.Any(p => p.Item1.ns1 == n.ns2 &&
                p.ns2 == n.Item1.ns1 && 
                p.Item1.ns1 != p.ns2 && !string.IsNullOrEmpty(p.Item1.ns1) 
                && !string.IsNullOrEmpty(p.ns2)))
                {
                    newMessages.Add(new DocumentMessage()
                    {
                        FileName = n.Item1.fileName,
                        RelativeFileName = n.Item1.fileName.Substring(folderBase.Length + 1),
                        LineNumber = n.Item1.lineNumber,
                        Text = "Circular reference " + n.Item1.ns1 + " and " + n.ns2 + " type = " + n.Item1.dt
                    }) ;
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

            void CheckEntities(string fileName, string folderBase, List<UMLOrderedEntity> entities, UMLSequenceDiagram o)
            {
                foreach (var g in entities)
                {
                    if (g.Warning != null && g is UMLSequenceConnection c)
                    {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = fileName,
                            RelativeFileName = fileName.Substring(folderBase.Length + 1),
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
                        CheckEntities(fileName, folderBase, s.Entities, o);
                    }
                }
            }
        }
    }
}