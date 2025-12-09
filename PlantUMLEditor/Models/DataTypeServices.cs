using PlantUML;
using SharpVectors.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UMLModels;
using static System.Net.Mime.MediaTypeNames;

namespace PlantUMLEditor.Models
{
    static class DataTypeServices
    {

        public static IEnumerable<GotoDefinitionResult> GotoDefinition(UMLDocumentCollection umlDocumentCollection, string text)
        {
            foreach (var item in umlDocumentCollection.ClassDocuments.Where(z =>
         z.DataTypes.Any(v => string.CompareOrdinal(v.NonGenericName, text) == 0))
                .Select(p => new GotoDefinitionResult(
                    p.FileName,
                    p,
                    p.DataTypes.First(z => z.NonGenericName == text)
                )))
            {
                yield return item;
            }
        }

        internal static IEnumerable<GlobalFindResult> FindAllReferences(UMLDocumentCollection documents,
            FindReferenceParam findParams)
        {
            List<UMLDataType> dataTypes = documents.ClassDocuments.SelectMany(z => z.DataTypes).ToList();

            var dataTypesById = documents.ClassDocuments
                .Select(z =>
                z.DataTypes.Where(z => z is UMLClass || z is UMLEnum || z is UMLInterface)
                .Select(p => new { FileName = z.FileName, DataType = p }))
                .SelectMany(z => z)
                .ToDictionary(z => z.DataType.Id, z => z);

            var methods = from o in documents.ClassDocuments
                          from dt in o.DataTypes
                          where dt.Methods.Any()
                          from m in dt.Methods
                          select new { FileName = o.FileName, Method = m, LineNumber = dt.LineNumber };

            var properties = from o in documents.ClassDocuments
                             from dt in o.DataTypes
                             where dt.Properties.Any()
                             from p in dt.Properties
                             where p.ObjectType is not null
                             where p.ObjectType.Id is not null
                             select new { FileName = o.FileName, Property = p, LineNumber = dt.LineNumber };



            var foundSequenceLine = from o in documents.SequenceDiagrams
                                    from e in o.FlattenedEntities
                                    where e is UMLSequenceConnection
                                   
                                    where ((UMLSequenceConnection)e).Line == findParams.line
                                    select new {FileName = o.FileName, Diagram = o, Connection = (UMLSequenceConnection)e };

            var foundSequenceLifeline = from o in documents.SequenceDiagrams
                                    from e in o.LifeLines
                               

                                    where ((UMLSequenceLifeline)e).Line == findParams.line
                                    select new { FileName = o.FileName, Diagram = o, Connection = (UMLSequenceLifeline)e };


            var sequenceLifeLines = from o in documents.SequenceDiagrams
                                    from c in o.LifeLines
                                    select new { FileName = o.FileName, LifeLine = c };

            var classesOrInterfaces = from o in documents.ClassDocuments
                                      from c in o.DataTypes
                                      select new { FileName = o.FileName, DataType = c };

       

            var firstFound = foundSequenceLine.FirstOrDefault();

            UMLDataType? dataTypeToFind = null;
            UMLSignature? signatureToFind = null;


            if (firstFound is not null)
            {
                if(UMLSequenceDiagramParser.TryParseAllConnections(findParams.line, firstFound.Diagram, 
                    documents.ClassDocuments.SelectMany(z => z.DataTypes).ToLookup(z => z.Alias),
                    null, 0, out UMLSequenceConnection? conn))
                {
                    if(conn.From.Alias == findParams.word)
                    {
                        dataTypeToFind = dataTypesById[conn.From.DataTypeId].DataType;
                    }

                    else if(conn.To.Alias == findParams.word)
                    {
                        dataTypeToFind = dataTypesById[conn.To.DataTypeId].DataType;
                    }

                    else if (conn.Action.Signature.Contains(findParams.word, StringComparison.Ordinal))
                    {
                        dataTypeToFind = dataTypesById[conn.To.DataTypeId].DataType;
                        signatureToFind = conn.Action;


                    }

                    if (dataTypeToFind is not null)
                    {
                        var classOrInterfaceRefMatches = from o in classesOrInterfaces
                                                         where o.DataType is UMLClass || o.DataType is UMLInterface
                                                         where string.CompareOrdinal( o.DataType.Id , dataTypeToFind.Id) == 0
                                                         select new GlobalFindResult(o.FileName, o.DataType.LineNumber, o.DataType.Name, findParams.word);
                        foreach (var item in classOrInterfaceRefMatches)
                        {
                            yield return item;
                        }

                      
                    }
                    if(dataTypeToFind is not null && signatureToFind is null)
                    {
                        var sequenceDiagramsMatching = from o in classesOrInterfaces
                                                       where o.DataType is UMLClass || o.DataType is UMLInterface
                                                       where string.CompareOrdinal(o.DataType.Id, dataTypeToFind.Id) == 0

                                                       from s in documents.SequenceDiagrams
                                                       from c in s.FlattenedEntities.OfType<UMLSequenceConnection>()
                                                       where c.To is not null && string.CompareOrdinal(c.To.DataTypeId, o.DataType.Id) == 0
                                                       || c.From is not null && string.CompareOrdinal(c.From.DataTypeId, o.DataType.Id) == 0

                                                       select new GlobalFindResult(s.FileName, c.LineNumber, c.Line, findParams.word);
                        foreach (var item in sequenceDiagramsMatching)
                        {
                            yield return item;
                        }
                    }
                    if(signatureToFind is not null && dataTypeToFind is not null)
                    {
                        var methodRefMatches = from o in classesOrInterfaces
                                               where o.DataType is UMLClass || o.DataType is UMLInterface
                                               where string.CompareOrdinal(o.DataType.Id , dataTypeToFind.Id) == 0
                                                  from m in o.DataType.Methods
                                                  where string.CompareOrdinal( m.Signature , signatureToFind.Signature) == 0
                                                  from s in documents.SequenceDiagrams
                                                from c in s.FlattenedEntities.OfType<UMLSequenceConnection>()
                                               where c.To is not null && string.CompareOrdinal( c.To.DataTypeId , o.DataType.Id) == 0
                                               where c.Action is not null && string.CompareOrdinal(c.Action.Signature, m.Signature) == 0
                                               select new GlobalFindResult(s.FileName, c.LineNumber, c.Line, findParams.word);
                        foreach (var item in methodRefMatches)
                        {
                            yield return item;
                        }

                        var propertyRefMatches = from o in classesOrInterfaces
                                               where o.DataType is UMLClass || o.DataType is UMLInterface
                                               where string.CompareOrdinal(o.DataType.Id, dataTypeToFind.Id) == 0
                                               from m in o.DataType.Properties
                                               where string.CompareOrdinal(m.Signature, signatureToFind.Signature) == 0
                                               from s in documents.SequenceDiagrams
                                               from c in s.FlattenedEntities.OfType<UMLSequenceConnection>()
                                               where c.To is not null && string.CompareOrdinal(c.To.DataTypeId, o.DataType.Id) == 0
                                               where c.Action is not null && string.CompareOrdinal(c.Action.Signature, m.Signature) == 0
                                               select new GlobalFindResult(s.FileName, c.LineNumber, c.Line, findParams.word);
                        foreach (var item in propertyRefMatches)
                        {
                            yield return item;
                        }

                        yield break;
                    }

                }

            }

            var firstLifeLineFound = foundSequenceLifeline.FirstOrDefault();
            if(firstLifeLineFound is not null)
            {
                var dataTypeToFind2 = dataTypesById[firstLifeLineFound.Connection.DataTypeId].DataType;

                if(dataTypeToFind2 is not null)
                {
                    var classOrInterfaceRefMatches = from o in classesOrInterfaces
                                                     where o.DataType is UMLClass || o.DataType is UMLInterface
                                                     where string.CompareOrdinal(o.DataType.Id, dataTypeToFind2.Id) == 0
                                                     select new GlobalFindResult(o.FileName, o.DataType.LineNumber, o.DataType.Name, findParams.word);
                    foreach (var item in classOrInterfaceRefMatches)
                    {
                        yield return item;
                    }

                    var classOrInterfaceMethodsRefMatches = from o in classesOrInterfaces
                                                     where o.DataType is UMLClass || o.DataType is UMLInterface
                                                     from m in o.DataType.Methods
                                                     where string.CompareOrdinal( m.ReturnType.Id, dataTypeToFind2.Id) == 0
                                                  
                                                     select new GlobalFindResult(o.FileName, o.DataType.LineNumber, m.Signature, findParams.word);
                    foreach (var item in classOrInterfaceMethodsRefMatches)
                    {
                        yield return item;
                    }


                    var classOrInterfaceMethodParamsRefMatches = from o in classesOrInterfaces
                                                            where o.DataType is UMLClass || o.DataType is UMLInterface
                                                            from m in o.DataType.Methods
                                                            from p in m.Parameters
                                                            where string.CompareOrdinal(p.ObjectType.Id, dataTypeToFind2.Id) == 0
                                                         
                                                            select new GlobalFindResult(o.FileName, o.DataType.LineNumber, p.Signature, findParams.word);
                    foreach (var item in classOrInterfaceMethodParamsRefMatches)
                    {
                        yield return item;
                    }


                    var classOrInterfacePropertyRefMatches = from o in classesOrInterfaces
                                                             where o.DataType is UMLClass || o.DataType is UMLInterface
                                                             from m in o.DataType.Properties
                                                             where string.CompareOrdinal(m.ObjectType.Id, dataTypeToFind2.Id) == 0

                                                             select new GlobalFindResult(o.FileName, o.DataType.LineNumber, m.Signature, findParams.word);
                    foreach (var item in classOrInterfacePropertyRefMatches)
                    {
                        yield return item;
                    }



                    var sequenceLifelinesMatching = from o in classesOrInterfaces
                                                   where o.DataType is UMLClass || o.DataType is UMLInterface
                                                   where string.CompareOrdinal(o.DataType.Id, dataTypeToFind2.Id) == 0

                                                   from s in documents.SequenceDiagrams
                                                   from c in s.FlattenedEntities.OfType<UMLSequenceLifeline>()
                                                   where c is not null && string.CompareOrdinal(c.DataTypeId, o.DataType.Id) == 0
                                                  
                                                   select new GlobalFindResult(s.FileName, c.LineNumber, c.Line, findParams.word);
                    foreach (var item in sequenceLifelinesMatching)
                    {
                        yield return item;
                    }


                    var sequenceDiagramsMatching = from o in classesOrInterfaces
                                                   where o.DataType is UMLClass || o.DataType is UMLInterface
                                                   where string.CompareOrdinal(o.DataType.Id, dataTypeToFind2.Id) == 0

                                                   from s in documents.SequenceDiagrams
                                                   from c in s.FlattenedEntities.OfType<UMLSequenceConnection>()
                                                   where c.To is not null && string.CompareOrdinal(c.To.DataTypeId, o.DataType.Id) == 0
                                                   || c.From is not null && string.CompareOrdinal(c.From.DataTypeId, o.DataType.Id) == 0

                                                   select new GlobalFindResult(s.FileName, c.LineNumber, c.Line, findParams.word);
                    foreach (var item in sequenceDiagramsMatching)
                    {
                        yield return item;
                    }
                }

                yield break;

            }




            var classOrInterfaceMatches = from o in classesOrInterfaces
                                          where o.DataType is UMLClass || o.DataType is UMLInterface
                                          where string.CompareOrdinal(o.DataType.Name, findParams.word) == 0
                                          select new GlobalFindResult(o.FileName, o.DataType.LineNumber, o.DataType.Name, findParams.word);





            foreach (var item in classOrInterfaceMatches)
            {
                yield return item;
            }


       
            
                


            var propertyMatches = from o in properties
                                    where string.CompareOrdinal(o.Property.Name, findParams.word) == 0
                                    select new GlobalFindResult(o.FileName, o.LineNumber, o.Property.Signature, findParams.word);

            foreach (var item in propertyMatches)
            {
                yield return item;
            }

            var propertyReturnMatches = from o in properties
                                  where string.CompareOrdinal(o.Property.ObjectType.Name, findParams.word) == 0
                                  select new GlobalFindResult(o.FileName, o.LineNumber, o.Property.Signature, findParams.word);

            foreach (var item in propertyReturnMatches)
            {
                yield return item;
            }

            var methodMatches = from o in methods
                                    where string.CompareOrdinal(o.Method.Name, findParams.word) == 0
                                    select new GlobalFindResult(o.FileName, o.LineNumber, o.Method.Signature, findParams.word);

            foreach (var item in methodMatches)
            {
                yield return item;
            }

            var methodReturnsMatches = from o in methods
                                where string.CompareOrdinal(o.Method.ReturnType.Name, findParams.word) == 0
                                select new GlobalFindResult(o.FileName, o.LineNumber, o.Method.Signature, findParams.word);

            foreach (var item in methodReturnsMatches)
            {
                yield return item;
            }


            var methodParamMatches = from o in methods
                                     from p in o.Method.Parameters
                                 
                                     where string.CompareOrdinal(p.Name, findParams.word) == 0
                                       select new GlobalFindResult(o.FileName, o.LineNumber, o.Method.Signature, findParams.word);

            foreach (var item in methodParamMatches)
            {
                yield return item;
            }

            var classMatches = from o in classesOrInterfaces
                                 where string.CompareOrdinal(o.DataType.NonGenericName, findParams.word) == 0
                                 select new GlobalFindResult(o.FileName, o.DataType.LineNumber, o.DataType.Name, findParams.word);

            foreach (var item in classMatches)
            {
                yield return item;
            }


                var lifeLineMatches = from o in sequenceLifeLines
                                      where o.LifeLine.DataTypeId != null
                                      let dt = dataTypesById.GetValueOrDefault(o.LifeLine.DataTypeId)
                                  where dt != null && string.CompareOrdinal(dt.DataType.NonGenericName, findParams.word) == 0
                                  select new GlobalFindResult(o.FileName, o.LifeLine.LineNumber, o.LifeLine.Text, findParams.word);

            foreach (var item in lifeLineMatches)
            {
                yield return item;
            }

            var classMethods = from o in dataTypes
                               from pm in o.Methods
                               where string.CompareOrdinal(pm.Signature, findParams.line) == 0
                               select new { DataType = o, Method = pm };

            var classProperties = from o in dataTypes
                                  from pm in o.Properties
                                  where string.CompareOrdinal(pm.Signature, findParams.line) == 0
                                  select new { DataType = o, Property = pm };

            var foundDt = classMethods.FirstOrDefault()?.DataType ?? classProperties.FirstOrDefault()?.DataType;
            var foundProp = classProperties.FirstOrDefault()?.Property;
            var foundMethod = classMethods.FirstOrDefault()?.Method;

            if (foundDt is not null)
            {

                var actionMatches = from o in documents.SequenceDiagrams
                                    from lc in o.FlattenedEntities.OfType<UMLSequenceConnection>()
                                    where lc is not null && lc.Action is not null && lc.To is not null &&
                                    string.CompareOrdinal(lc.To.DataTypeId, foundDt.Id) == 0 &&
                                    (string.CompareOrdinal(lc.Action.Signature, foundProp?.Signature) == 0
                                    || string.CompareOrdinal(lc.Action.Signature, foundMethod?.Signature) == 0)
                                   select new GlobalFindResult(o.FileName, lc.LineNumber, lc!.Action!.Signature, lc!.Action!.Signature);

                foreach (var item in actionMatches)
                {
                    yield return item;
                }

                //var typeMatches = from o in documents.SequenceDiagrams
                //                    from lc in o.FlattenedEntities.OfType<UMLSequenceConnection>()
                //                    where lc is not null &&
                //                    (string.CompareOrdinal(lc.To?.DataTypeId, foundDt.Id) == 0 ||
                //                    string.CompareOrdinal(lc.From?.DataTypeId, foundDt.Id) == 0)
                                     
                //                    select new GlobalFindResult(o.FileName, lc.LineNumber, lc.Line, lc.Line);

                //foreach (var item in typeMatches)
                //{
                //    yield return item;
                //}

            }




            /*



            foreach (var item in documents.ClassDocuments.Where(z =>
         z.DataTypes.Any(v => string.CompareOrdinal(v.NonGenericName, text) == 0)).Select(p => new
         {
             FN = p.FileName,
             D = p,
             DT = p.DataTypes.First(z => z.NonGenericName == text)
         }))
            {
                yield return new GlobalFindResult(item.FN, item.DT.LineNumber, item.DT.Name, text);

                foreach (GlobalFindResult? ln in documents.ClassDocuments.SelectMany(z => z.DataTypes.SelectMany(x => x.Properties.Where(g =>
                DocumentMessageGenerator.GetCleanTypes(dataTypes, g.ObjectType.Name).Contains(item.DT.NonGenericName))
                .Select(g => new GlobalFindResult(z.FileName, x.LineNumber, g.Signature, text)))))
                {
                    yield return ln;
                }
                foreach (GlobalFindResult? ln in documents.ClassDocuments.SelectMany(z => z.DataTypes.SelectMany(x => x.Methods.SelectMany(k => k.Parameters.Where(g =>
                  DocumentMessageGenerator.GetCleanTypes(dataTypes, g.ObjectType.Name).Contains(item.DT.NonGenericName))
              .Select(g => new GlobalFindResult(z.FileName, x.LineNumber, k.Signature, text))))))
                {
                    yield return ln;
                }
                foreach (GlobalFindResult? ln in documents.ClassDocuments.SelectMany(z => z.DataTypes.SelectMany(x => x.Methods.Where(k =>
                DocumentMessageGenerator.GetCleanTypes(dataTypes, k.ReturnType.Name).Contains(item.DT.NonGenericName))
           .Select(g => new GlobalFindResult(z.FileName, x.LineNumber, g.Signature, text)))))
                {
                    yield return ln;
                }

                foreach (GlobalFindResult? ln in documents.SequenceDiagrams.SelectMany(z => z.LifeLines.Where(x => x.DataTypeId == item.DT.NonGenericName).Select(c =>
                new GlobalFindResult(z.FileName, c.LineNumber, c.Text, text))))

                {
                    yield return ln;
                }
                foreach (GlobalFindResult? ln in documents.SequenceDiagrams.SelectMany(z => z.Entities.OfType<UMLSequenceConnection>()
                .Where(x => x.From?.DataTypeId == item.DT.Id || x.To?.DataTypeId == item.DT.Id).Where(c => c.Action is not null).Select(c =>
               new GlobalFindResult(z.FileName, c.LineNumber, c.Action!.Signature, text))))

                {
                    yield return ln;
                }
            }
            */
        }

        public class GotoDefinitionResult
        {
            public GotoDefinitionResult(string fileName, UMLClassDiagram diagram, UMLDataType dataType)
            {
                FileName = fileName;
                Diagram = diagram;
                DataType = dataType;
            }

            public string FileName { get; set; }
            public UMLClassDiagram Diagram { get; set; }
            public UMLDataType DataType { get; set; }
        }
    }
}
