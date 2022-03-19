using System.Threading.Tasks;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class UMLDiagramTypeDiscovery
    {
        public static async Task<UMLClassDiagram?> TryCreateClassDiagram(UMLDocumentCollection documents, string fullPath)
        {
            if (!fullPath.Contains("class.puml", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            UMLClassDiagram? cd = documents.ClassDocuments.Find(p => p.FileName == fullPath);
            if (cd == null)
            {
                try
                {
                    cd = await PlantUML.UMLClassDiagramParser.ReadFile(fullPath);
                }
                catch
                {
                }
                if (cd != null)
                {
                    documents.ClassDocuments.RemoveAll(p => p.FileName == fullPath);
                    documents.ClassDocuments.Add(cd);
                }
            }

            return cd;
        }

        public static async Task<UMLComponentDiagram?> TryCreateComponentDiagram(UMLDocumentCollection documents, string fullPath)
        {
            if (!fullPath.Contains("component.puml", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            UMLComponentDiagram? cd = documents.ComponentDiagrams.Find(p => p.FileName == fullPath);
            if (cd == null)
            {
                try
                {
                    cd = await PlantUML.UMLComponentDiagramParser.ReadFile(fullPath);
                }
                catch
                {
                }
                if (cd != null)
                {
                    documents.ComponentDiagrams.RemoveAll(p => p.FileName == fullPath);
                    documents.ComponentDiagrams.Add(cd);
                }
            }

            return cd;
        }

        public static async Task<(UMLClassDiagram? cd, UMLSequenceDiagram? sd, UMLUnknownDiagram? ud)> TryCreateDiagram(UMLDocumentCollection documents, string text)
        {
            UMLUnknownDiagram? ud = null;
            UMLClassDiagram? cd = null;
            UMLSequenceDiagram? sd = await PlantUML.UMLSequenceDiagramParser.ReadString(text, documents.ClassDocuments, false);
            if (sd == null)
            {
                cd = await PlantUML.UMLClassDiagramParser.ReadString(text);
            }

            if (cd == null)
            {
                ud = new UMLUnknownDiagram("", "");
            }

            return (cd, sd, ud);
        }

        public static async Task<UMLSequenceDiagram?> TryCreateSequenceDiagram(UMLDocumentCollection documents, string fullPath)
        {
            if (!fullPath.Contains("seq.puml", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            UMLSequenceDiagram? sd = documents.SequenceDiagrams.Find(p => p.FileName == fullPath);
            if (sd == null)
            {
                try
                {
                    sd = await PlantUML.UMLSequenceDiagramParser.ReadFile(fullPath, documents.ClassDocuments, false);
                }
                catch { }
                if (sd != null)
                {
                    documents.SequenceDiagrams.RemoveAll(p => p.FileName == fullPath);
                    documents.SequenceDiagrams.Add(sd);
                }
            }

            return sd;
        }

        public static async Task<(UMLClassDiagram? cd, UMLSequenceDiagram? sd, UMLComponentDiagram? comd, UMLUnknownDiagram? ud)> TryFindOrAddDocument(UMLDocumentCollection documents, string fullPath)
        {
            UMLUnknownDiagram? ud = null;
            UMLClassDiagram? cd = null;
            UMLComponentDiagram? comd = null;
            UMLSequenceDiagram? sd = await TryCreateSequenceDiagram(documents, fullPath);
            if (sd == null)
            {
                cd = await TryCreateClassDiagram(documents, fullPath);
            }

            if (cd == null)
            {
                comd = await TryCreateComponentDiagram(documents, fullPath);
            }

            if (cd == null)
            {
                ud = await PlantUML.UnknownDiagramParser.ReadFile(fullPath);
            }

            return (cd, sd, comd, ud);
        }
    }
}