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
            if (string.IsNullOrWhiteSpace(name))
                return Array.Empty<string>();

            // Split on generic delimiters, then take the last segment of any dotted name and remove empties.
            // For example:
            //  - "HashSet<string>" -> ["HashSet", "string"]
            //  - "List<HashSet<string>>" -> ["List", "HashSet", "string"]
            var parts = name
                .Split(GENERICSPLITS, StringSplitOptions.RemoveEmptyEntries)
                .Select(z => z.Trim())
                .Where(z => !string.IsNullOrEmpty(z))
                .Select(z =>
                {
                    var segs = z.Split(seperators, StringSplitOptions.RemoveEmptyEntries);
                    return segs.Length > 0 ? segs[^1].Trim() : string.Empty;
                })
                .Where(z => !string.IsNullOrEmpty(z))
                .Distinct()
                .ToArray();

            return parts;


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
                        newMessages.Add(new DocumentMessage(f.FileName, GetRelativeName(folderBase, f.FileName),
                            LineNumber, Line + " " + message, false));

                    }
                }
                if (doc is UMLClassDiagram f2)
                {
                    foreach (UMLDataType? fdt in f2.DataTypes)
                    {
                        // Exact-name duplicate (including generic arguments)
                        foreach(var d in dataTypes.Where(z=> z.DataType is not UMLOther &&
                        z.DataType.Namespace == fdt.Namespace &&
                        string.CompareOrdinal(z.DataType.Name, fdt.Name) == 0))
                        {
                            newMessages.Add(new DocumentMessage(f2.FileName, 
                                GetRelativeName(folderBase, f2.FileName),
                                fdt.LineNumber, "Duplicate type " + fdt.Name, false));

                            newMessages.Add(new DocumentMessage(d.FileName,
                                GetRelativeName(folderBase, d.FileName),
                                d.DataType.LineNumber, "Duplicate type " + fdt.Name, false));
                        }

                        // Same base non-generic name but different actual type (e.g. HashSet and HashSet<string>)
                  //      foreach (var d in dataTypes.Where(z =>
                  //z.DataType.NonGenericName == fdt.NonGenericName &&
                  //string.CompareOrdinal(z.DataType.Name, fdt.Name) != 0))
                  //      {
                  //          newMessages.Add(new DocumentMessage(f2.FileName,
                  //              GetRelativeName(folderBase, f2.FileName),
                  //              fdt.LineNumber, "Same name " + fdt.NonGenericName, true));

                  //          newMessages.Add(new DocumentMessage(d.FileName,
                  //              GetRelativeName(folderBase, d.FileName),
                  //              d.DataType.LineNumber, "Same name " + fdt.NonGenericName, true));
                  //      }


                        dataTypes.Add(new(fdt, f2.FileName));
                    }

                    foreach (UMLError? e in f2.Errors)
                    {
                        newMessages.Add(new DocumentMessage(f2.FileName,
                            GetRelativeName(folderBase, f2.FileName),
                            e.LineNumber, e.Value, false));

                    }
                }
                if (doc is UMLSequenceDiagram o)
                {
                    ValidateSequenceDiagram(folderBase, newMessages, doc, o);
                }

                AddLineErrors(folderBase, doc, newMessages);
            }



            List<DataTypeToNamespaceMapping>? namespaceReferences = FindBadDataTypes(folderBase, newMessages,
                dataTypes.OrderBy(p => p.DataType.Name.Length).ToList());


            FindCircularReferences(folderBase, newMessages, namespaceReferences);

            return newMessages;

        }

        private void AddLineErrors(string folderBase, UMLDiagram doc, List<DocumentMessage> newMessages)
        {
            foreach (LineError? lineError in doc.LineErrors)
            {
                if(lineError == null)
                    continue;
                newMessages.Add(new DocumentMessage(doc.FileName,
                    GetRelativeName(folderBase, doc.FileName), lineError.LineNumber, lineError.Text, false));
            }
        }


        private void ValidateSequenceDiagram(string folderBase, List<DocumentMessage> newMessages,
            UMLDiagram doc, UMLSequenceDiagram o)
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

                    newMessages.Add(new DocumentMessage(i.o.FileName, 
                        GetRelativeName(folderBase, i.o.FileName), i.f.LineNumber, 
                        i.f.Warning ?? "NULL WARNING", false));

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
                        newMessages.Add(new MissingMethodDocumentMessage(fileName, 
                            GetRelativeName(folderBase, fileName), g.LineNumber,
                            g.Warning, false, c.Action.Signature,
                            c.To.DataTypeId, o, true));
                    }
                    else
                    {
                        newMessages.Add(new DocumentMessage(fileName,
                            GetRelativeName(folderBase, fileName), g.LineNumber, g.Warning, false));
                    }
                }

                if (g is UMLSequenceBlockSection s)
                {
                    CheckEntities(fileName, folderBase, s.Entities, o, newMessages);
                }
            }
        }

        private static void FindCircularReferences(string folderBase, List<DocumentMessage> newMessages,
            List<DataTypeToNamespaceMapping> namespaceReferences)
        {
     

            foreach (DataTypeToNamespaceMapping? n in namespaceReferences)
            {
                if (namespaceReferences.Any(p => 
                string.CompareOrdinal(p.DataTypePointer.NS1, n.NS2) == 0 &&
                string.CompareOrdinal(p.NS2, n.DataTypePointer.NS1) == 0 &&
                string.CompareOrdinal(p.DataTypePointer.NS1, p.NS2) != 0 &&
                !string.IsNullOrEmpty(p.DataTypePointer.NS1)
                && !string.IsNullOrEmpty(p.NS2)))
                {
                    string text = "Circular reference " + n.DataTypePointer.NS1 + " and " + n.NS2 + " type = " + n.DataTypePointer.DataType + " offender " + n.DataTypePointer.Name;

                    newMessages.Add(new DocumentMessage(n.DataTypePointer.FileName,
                        GetRelativeName(folderBase, n.DataTypePointer.FileName), 
                        n.DataTypePointer.LineNumber, text));

                }
            }
        }

        private record DataTypeReference(string FileName, int LineNumber, string NS1, string DataType, string Name);
        private record DataTypeToNamespaceMapping(DataTypeReference DataTypePointer, string NS2);

        private static List<DataTypeToNamespaceMapping>
            FindBadDataTypes(string folderBase, List<DocumentMessage> newMessages,
             List<DataTypeRecord> dataTypes)
        {
            List<DataTypeToNamespaceMapping> namespaceReferences = new();

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
                        DataTypeRecord? pdt = dataTypes.FirstOrDefault(z => string.CompareOrdinal(z.DataType.NonGenericName, r) == 0);
                        ProcessPDT(folderBase, newMessages, namespaceReferences, dt, m.Name, r, pdt);
                    }
                }
                foreach (UMLMethod? m in dt.DataType.Methods)
                {
                    foreach (UMLParameter? p in m.Parameters)
                    {

                        string[] parsedTypes = GetCleanTypes(dataTypes, p.ObjectType.Name);
                        foreach (string? r in parsedTypes)
                        {
                            DataTypeRecord? pdt = dataTypes
                                 .FirstOrDefault(z => GetCleanTypes(dataTypes, z.DataType.Name).Contains(r));
                            ProcessPDT(folderBase, newMessages, namespaceReferences, dt, m.Name, r, pdt);
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(m.ReturnType.Name))
                    {

                        string[] parsedTypes = GetCleanTypes(dataTypes, m.ReturnType.Name);
                        foreach (string? r in parsedTypes)
                        {
                            DataTypeRecord? pdt = dataTypes
                                .FirstOrDefault(z => GetCleanTypes(dataTypes, z.DataType.Name).Contains(r));
                            ProcessPDT(folderBase, newMessages, namespaceReferences, dt, m.Name, r, pdt);
                        }
                    }
                }
            }

            return namespaceReferences;

            static void ProcessPDT(string folderBase, List<DocumentMessage> newMessages, List<DataTypeToNamespaceMapping> namespaceReferences,
                DataTypeRecord dt, string name, string dataType, DataTypeRecord? pdt)
            {
                // If we don't have a matching type, skip reporting for known framework/collection names.
                string simple = dataType;
                if (simple.Contains('.'))
                    simple = simple.Split('.').Last();

                if (pdt == default)
                {
                    if (knownwords.Contains(simple))
                    {
                        // known framework/collection type - do not report as missing
                        return;
                    }

                    newMessages.Add(new MissingDataTypeMessage(dt.FileName, GetRelativeName(folderBase, dt.FileName),
                    dt.DataType.LineNumber, dataType + " used by " + name, false, dataType, false));


                }
                else
                {
                    if(!string.IsNullOrEmpty(pdt.DataType.Namespace))
                        namespaceReferences.Add(new(new(dt.FileName, dt.DataType.LineNumber, dt.DataType.Namespace, dataType, name), pdt.DataType.Namespace));

                }
            }
        }

        private static string GetRelativeName(string folderBase, string fullPath)
        {
            return fullPath[(folderBase.Length + 1)..];
        }
    }
    

}