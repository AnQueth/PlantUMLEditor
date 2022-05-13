using System;
using System.Collections.Generic;
using System.Linq;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class DocumentMessageGenerator
    {
        private readonly IEnumerable<UMLDiagram> documents;
        private static readonly string[] knownwords = {"Task", "List", "IReadOnlyCollection",
                "IList", "IEnumerable", "Dictionary", "out", "var", "HashSet","IEnumerableTask", "IHandler",
        "A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z"
        };
        private static readonly char[] seperators = { ' ', '.', ',', '<', '>', '[', ']' };

        public DocumentMessageGenerator(IEnumerable<UMLDiagram> documents)
        {
            this.documents = documents;


        }

        private static readonly char[] GENERICSPLITS = new char[] { '<', '>', ',', '[', ']', '?' };

        public static string[] GetCleanTypes(IEnumerable<UMLDataType> dataTypes, string name)
        {
            var types = name.Split(GENERICSPLITS, StringSplitOptions.RemoveEmptyEntries).Select(z => z.Trim())

                .ToArray();
            return types;


        }
        private static string[] GetCleanTypes(List<DataTypeRecord> dataTypes, string name)
        {
            return GetCleanTypes(dataTypes.Select(z => z.DataType), name);
            //           .Where(z => !dataTypes.Any(x => string.CompareOrdinal(x.DataType.NonGenericName, z) == 0)).ToArray();





        }

        private record DataTypeRecord(UMLDataType DataType, string FileName);

        public List<DocumentMessage> Generate(string folderBase)
        {
            List<DocumentMessage> newMessages = new();

            List<DataTypeRecord> dataTypes = new();

            foreach (UMLDiagram? doc in documents)
            {
                if (doc is UMLComponentDiagram f)
                {
                    foreach ((string Line, int LineNumber, string message) in f.ExplainedErrors)
                    {
                        newMessages.Add(new DocumentMessage(f.FileName, GetRelativeName(folderBase, f.FileName), LineNumber, Line + " " + message, false));

                    }
                }
                if (doc is UMLClassDiagram f2)
                {
                    foreach (UMLDataType? fdt in f2.DataTypes)
                    {
                        dataTypes.Add(new(fdt, f2.FileName));
                    }

                    foreach (UMLError? e in f2.Errors)
                    {
                        newMessages.Add(new DocumentMessage(f2.FileName, GetRelativeName(folderBase, f2.FileName), e.LineNumber, e.Value, false));

                    }
                }
                if (doc is UMLSequenceDiagram o)
                {
                    ValidateSequenceDiagram(folderBase, newMessages, doc, o);
                }

                AddLineErrors(folderBase, doc, newMessages);
            }



            List<BadDataTypeAndNS>? namespaceReferences = FindBadDataTypes(folderBase, newMessages,
                dataTypes.OrderByDescending(p => p.DataType.Name.Length).ToList());


            FindCircularReferences(folderBase, newMessages, namespaceReferences);

            return newMessages;

        }

        private void AddLineErrors(string folderBase, UMLDiagram doc, List<DocumentMessage> newMessages)
        {
            foreach (LineError? lineError in doc.LineErrors)
            {
                newMessages.Add(new DocumentMessage(doc.FileName,
                    GetRelativeName(folderBase, doc.FileName), lineError.LineNumber, lineError.Text, true));
            }
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

        private void CheckEntities(string fileName, string folderBase,
            List<UMLOrderedEntity> entities, UMLSequenceDiagram o,
            List<DocumentMessage> newMessages)
        {
            foreach (UMLOrderedEntity? g in entities)
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
            foreach (BadDataTypeAndNS? n in namespaceReferences)
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

            foreach (DataTypeRecord? dt in dataTypes)
            {
                if (dt.DataType is UMLEnum)
                {
                    continue;
                }

                foreach (UMLProperty? m in dt.DataType.Properties)
                {


                    string[] parsedTypes = GetCleanTypes(dataTypes, m.ObjectType.Name);
                    foreach (string? r in parsedTypes)
                    {
                        DataTypeRecord? pdt = dataTypes.FirstOrDefault(z => string.CompareOrdinal(z.DataType.Name, r) == 0);
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
                foreach (UMLMethod? m in dt.DataType.Methods)
                {
                    foreach (UMLParameter? p in m.Parameters)
                    {

                        string[] parsedTypes = GetCleanTypes(dataTypes, p.ObjectType.Name);
                        foreach (string? r in parsedTypes)
                        {
                            DataTypeRecord? pdt = dataTypes.FirstOrDefault(z => string.CompareOrdinal(z.DataType.Name, r) == 0);
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

                        string[] parsedTypes = GetCleanTypes(dataTypes, m.ReturnType.Name);
                        foreach (string? r in parsedTypes)
                        {
                            DataTypeRecord? pdt = dataTypes
                                .FirstOrDefault(z => GetCleanTypes(dataTypes, z.DataType.Name).Contains(r));
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