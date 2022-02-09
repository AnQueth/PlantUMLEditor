﻿using System;
using System.Collections.Generic;
using System.Linq;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class DocumentMessageGenerator
    {
        private readonly IEnumerable<UMLDiagram> documents;

        static readonly string[] knownwords = {"Task", "List", "IReadOnlyCollection",
                "IList", "IEnumerable", "Dictionary", "out", "var", "HashSet","IEnumerableTask", "IHandler",
        "A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z"
        };
        static readonly char[] seperators = { ' ', '.', ',', '<', '>', '[', ']' };

        public DocumentMessageGenerator(IEnumerable<UMLDiagram> documents)
        {
            this.documents = documents;


        }

        private static List<string> GetCleanName(List<DataTypeRecord> dataTypes, string name)
        {
            List<string> types = new();
            foreach (var type in dataTypes.OrderByDescending(p => p.DataType.Name.Length))
            {
                if (name.IndexOf(type.DataType.Name, StringComparison.Ordinal) >= 0)
                {
                    types.Add(type.DataType.Name);

                    name = name.Replace(type.DataType.Name, string.Empty);
                }
            }

            string[] parts = name.Split(seperators);


            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p) || knownwords.Contains(p, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                types.Add(p);

            }

            return types;



        }

        private record DataTypeRecord(UMLDataType DataType, string FileName);

        public List<DocumentMessage> Generate(string folderBase)
        {
            List<DocumentMessage> newMessages = new();

            List<DataTypeRecord> dataTypes = new();

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
                        dataTypes.Add(new(fdt, f2.FileName));
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
                            select new
                            {
                                o = doc,
                                f = z
                            };

                foreach (var i in items)
                {

                    newMessages.Add(new DocumentMessage(i.o.FileName, GetRelativeName(folderBase, i.o.FileName), i.f.LineNumber, i.f.Warning ?? "NULL WARNING", true));

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
                if (g.Warning is not null && g is UMLSequenceConnection c)
                {
                    if (c.Action is not null && c.To is not null && c.To.DataTypeId is not null && c.From is not null)
                    {
                        newMessages.Add(new MissingMethodDocumentMessage(fileName, GetRelativeName(folderBase, fileName), g.LineNumber, g.Warning, true, c.Action.Signature,
                            c.To.DataTypeId, o, true));
                    }
                    else
                    {
                        newMessages.Add(new DocumentMessage(fileName, GetRelativeName(folderBase, fileName), g.LineNumber, g.Warning, true));
                    }
                }

                if (g is UMLSequenceBlockSection s)
                {
                    CheckEntities(fileName, folderBase, s.Entities, o, newMessages);
                }
            }
        }

        private static void FindCircularReferences(string folderBase, List<DocumentMessage> newMessages,
            List<BadDataTypeAndNS> namespaceReferences)
        {
            foreach (var n in namespaceReferences)
            {
                if (namespaceReferences.Any(p => string.CompareOrdinal(p.BadDataType.NS1, n.NS2) == 0 &&
                string.CompareOrdinal(p.NS2, n.BadDataType.NS1) == 0 &&
                string.CompareOrdinal(p.BadDataType.NS1, p.NS2) != 0 &&
                !string.IsNullOrEmpty(p.BadDataType.NS1)
                && !string.IsNullOrEmpty(p.NS2)))
                {
                    string text = "Circular reference " + n.BadDataType.NS1 + " and " + n.NS2 + " type = " + n.BadDataType.DataType + " offender " + n.BadDataType.Name;

                    newMessages.Add(new DocumentMessage(n.BadDataType.FileName, GetRelativeName(folderBase, n.BadDataType.FileName), n.BadDataType.LineNumber, text));

                }
            }
        }

        private record BadDataType(string FileName, int LineNumber, string NS1, string DataType, string Name);
        private record BadDataTypeAndNS(BadDataType BadDataType, string NS2);

        private static List<BadDataTypeAndNS>
            FindBadDataTypes(string folderBase, List<DocumentMessage> newMessages,
             List<DataTypeRecord> dataTypes)
        {
            List<BadDataTypeAndNS> namespaceReferences = new();

            foreach (var dt in dataTypes)
            {
                if (dt.DataType is UMLEnum)
                {
                    continue;
                }

                foreach (var m in dt.DataType.Properties)
                {


                    var parsedTypes = GetCleanName(dataTypes, m.ObjectType.Name);
                    foreach (var r in parsedTypes)
                    {
                        var pdt = dataTypes.FirstOrDefault(z => string.CompareOrdinal(z.DataType.Name, r) == 0);
                        if (pdt == default)
                        {
                            newMessages.Add(new MissingDataTypeMessage(dt.FileName, GetRelativeName(folderBase, dt.FileName),
                                dt.DataType.LineNumber, r + " used by " + m.Name, true, r, true));

                        }
                        else
                        {
                            namespaceReferences.Add(new(new(dt.FileName, dt.DataType.LineNumber, dt.DataType.Namespace, r, m.Name), pdt.DataType.Namespace));
                        }
                    }
                }
                foreach (var m in dt.DataType.Methods)
                {
                    foreach (var p in m.Parameters)
                    {

                        var parsedTypes = GetCleanName(dataTypes, p.ObjectType.Name);
                        foreach (var r in parsedTypes)
                        {
                            var pdt = dataTypes.FirstOrDefault(z => string.CompareOrdinal(z.DataType.Name, r) == 0);
                            if (pdt == default)
                            {
                                newMessages.Add(new MissingDataTypeMessage(dt.FileName, GetRelativeName(folderBase, dt.FileName),
                                   dt.DataType.LineNumber, r + " used by " + m.Name, true, r, true));

                            }
                            else
                            {
                                namespaceReferences.Add(new(new(dt.FileName, dt.DataType.LineNumber, dt.DataType.Namespace, r, m.Name), pdt.DataType.Namespace));

                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(m.ReturnType.Name))
                    {

                        var parsedTypes = GetCleanName(dataTypes, m.ReturnType.Name);
                        foreach (var r in parsedTypes)
                        {
                            var pdt = dataTypes
                                .FirstOrDefault(z => GetCleanName(dataTypes, z.DataType.Name).Contains(r));
                            if (pdt == default)
                            {
                                newMessages.Add(new MissingDataTypeMessage(dt.FileName, GetRelativeName(folderBase, dt.FileName),
                                dt.DataType.LineNumber, r + " used by " + m.Name, true, r, true));


                            }
                            else
                            {
                                namespaceReferences.Add(new(new(dt.FileName, dt.DataType.LineNumber, dt.DataType.Namespace, r, m.Name), pdt.DataType.Namespace));

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