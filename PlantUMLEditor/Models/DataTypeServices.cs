using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UMLModels;
using static System.Net.Mime.MediaTypeNames;

namespace PlantUMLEditor.Models
{
    class DataTypeServices
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

        internal static IEnumerable<GlobalFindResult> FindAllReferences(UMLDocumentCollection documents, string text)
        {

            List<UMLDataType> dataTypes = documents.ClassDocuments.SelectMany(z => z.DataTypes).ToList();

         
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
