using System;
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
        private readonly IEnumerable<UMLDiagram> documents;

        static readonly string[] knownwords = {"Task", "List", "IReadOnlyCollection",
                "IList", "IEnumerable", "Dictionary", "out", "var", "HashSet","IEnumerableTask", "IHandler"};
        static readonly char[] seperators = { ' ', '.', ',', '<', '>', '[', ']' };

        public DocumentMessageGenerator(IEnumerable<UMLDiagram> documents)
        {
            this.documents = documents;

        }

        private static List<string> GetCleanName(string name)
        {


            string[] parts = name.Split(seperators);

            List<string> types = new();
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p) || knownwords.Contains(p, StringComparer.OrdinalIgnoreCase))
                    continue;

                types.Add(p);

            }

            return types;



        }

        public List<DocumentMessage> Generate(string folderBase)
        {
            List<DocumentMessage> newMessages = new();

            List<(UMLDataType, string)> dataTypes = new();

            foreach (var doc in documents)
            {
                if (doc is UMLComponentDiagram f)
                {
                    foreach (var (Line, LineNumber, message) in f.Errors)
                    {
                        newMessages.Add(new DocumentMessage(f.FileName, GetRelativeName(folderBase, f.FileName), LineNumber, Line + " " + message, false));

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
                        newMessages.Add(new DocumentMessage(f2.FileName, GetRelativeName(folderBase, f2.FileName), e.LineNumber, e.Value, false));

                    }
                }
                if (doc is UMLSequenceDiagram o)
                {
                    ValidateSequenceDiagram(folderBase, newMessages, doc, o);
                }
            }

            var namespaceReferences = FindBadDataTypes(folderBase, newMessages, dataTypes);

            FindCircularReferences(folderBase, newMessages, namespaceReferences);

            return newMessages;

        }

        private void ValidateSequenceDiagram(string folderBase, List<DocumentMessage> newMessages, UMLDiagram doc, UMLSequenceDiagram o)
        {
            if (o.ValidateAgainstClasses)
            {
                var items = from z in o.LifeLines
                            where z.Warning != null
                            select new { o = doc, f = z };

                foreach (var i in items)
                {

                    newMessages.Add(new DocumentMessage(i.o.FileName, GetRelativeName(folderBase, i.o.FileName), i.f.LineNumber, i.f.Warning, true));

                }

                CheckEntities(o.FileName, folderBase, o.Entities, o, newMessages);

            }
        }

        void CheckEntities(string fileName, string folderBase,
            List<UMLOrderedEntity> entities, UMLSequenceDiagram o,
            List<DocumentMessage> newMessages)
        {
            foreach (var g in entities)
            {
                if (g.Warning != null && g is UMLSequenceConnection c)
                {
                    if (c.Action != null && c.To != null)
                    {
                        newMessages.Add(new MissingMethodDocumentMessage(fileName, GetRelativeName(folderBase, fileName), g.LineNumber, g.Warning, true, c.Action.Signature,
                            c.To.DataTypeId, o, true));
                    }
                }

                if (g is UMLSequenceBlockSection s)
                {
                    CheckEntities(fileName, folderBase, s.Entities, o, newMessages);
                }
            }
        }

        private static void FindCircularReferences(string folderBase, List<DocumentMessage> newMessages,
            List<((string fileName, int lineNumber, string ns1, string dt, string name), string ns2)> namespaceReferences)
        {
            foreach (var n in namespaceReferences)
            {
                if (namespaceReferences.Any(p => string.CompareOrdinal(p.Item1.ns1, n.ns2) == 0 &&
                string.CompareOrdinal(p.ns2, n.Item1.ns1) == 0 &&
                string.CompareOrdinal(p.Item1.ns1, p.ns2) != 0 &&
                !string.IsNullOrEmpty(p.Item1.ns1)
                && !string.IsNullOrEmpty(p.ns2)))
                {
                    string text = "Circular reference " + n.Item1.ns1 + " and " + n.ns2 + " type = " + n.Item1.dt + " offender " + n.Item1.name;

                    newMessages.Add(new DocumentMessage(n.Item1.fileName, GetRelativeName(folderBase, n.Item1.fileName), n.Item1.lineNumber, text)) ;

                }
            }
        }

        private static List<((string fileName, int lineNumber, string ns1, string dt, string name), string ns2)> FindBadDataTypes(string folderBase, List<DocumentMessage> newMessages, List<(UMLDataType, string)> dataTypes)
        {
            List<((string fileName, int lineNumber, string ns1, string dt, string name), string ns2)> namespaceReferences
                = new();

            foreach (var dt in dataTypes)
            {
                if (dt.Item1 is UMLEnum)
                    continue;
                foreach (var m in dt.Item1.Properties)
                {
                    var parsedTypes = GetCleanName(m.ObjectType.Name);
                    foreach (var r in parsedTypes)
                    {
                        var pdt = dataTypes.FirstOrDefault(z => string.CompareOrdinal(z.Item1.Name, r) == 0);
                        if (pdt == default)
                        {
                            newMessages.Add(new MissingDataTypeMessage(dt.Item2, GetRelativeName(folderBase, dt.Item2),
                                dt.Item1.LineNumber, r + " used by " + m.Name, true, r, true));

                        }
                        else
                        {
                            namespaceReferences.Add(((dt.Item2, dt.Item1.LineNumber, dt.Item1.Namespace, r, m.Name), pdt.Item1.Namespace));
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
                            var pdt2 = dataTypes.FirstOrDefault(z => string.CompareOrdinal(z.Item1.Name, r) == 0);
                            if (pdt2 == default)
                            {
                                newMessages.Add(new MissingDataTypeMessage(dt.Item2, GetRelativeName(folderBase, dt.Item2),
                                   dt.Item1.LineNumber, r + " used by " + m.Name, true, r, true));

                            }
                            else
                            {
                                namespaceReferences.Add(((dt.Item2, dt.Item1.LineNumber, dt.Item1.Namespace, r, m.Name), pdt2.Item1.Namespace));

                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(m.ReturnType.Name))
                    {
                        var parsedTypes = GetCleanName(m.ReturnType.Name);
                        foreach (var r in parsedTypes)
                        {
                            var pdt = dataTypes
                                .FirstOrDefault(z => GetCleanName(z.Item1.Name).Contains(r));
                            if (pdt == default)
                            {
                                newMessages.Add(new MissingDataTypeMessage(dt.Item2, GetRelativeName(folderBase, dt.Item2),
                                dt.Item1.LineNumber, r + " used by " + m.Name, true, r, true));


                            }
                            else
                            {
                                namespaceReferences.Add(((dt.Item2, dt.Item1.LineNumber, dt.Item1.Namespace, r, m.Name), pdt.Item1.Namespace));

                            }
                        }
                    }
                }
            }

            return namespaceReferences;
        }

        private static string GetRelativeName(string folderBase, string fullPath)
        {
            return fullPath[(folderBase.Length + 1)..];
        }
    }
}