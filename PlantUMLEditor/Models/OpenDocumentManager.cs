using PlantUMLEditor.Models.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Controls;
using UMLModels;

namespace PlantUMLEditor.Models
{
    class OpenDocumentManager
    {
        private readonly ObservableCollection<BaseDocumentModel> _openDocuments;
        private readonly UMLDocumentCollection _umlDocumentCollection;
        private readonly object _docLock;
        private readonly IIOService _ioService;
        private readonly Channel<bool> _messageCheckerTrigger;

        public OpenDocumentManager(ObservableCollection<BaseDocumentModel> openDocuments,
            UMLDocumentCollection umlDocumentCollection,
            object docLock, IIOService ioService, Channel<bool> messageCheckerTrigger)
        {
            this._openDocuments = openDocuments;
            this._umlDocumentCollection = umlDocumentCollection;
            _docLock = docLock;
            _ioService = ioService;
            _messageCheckerTrigger = messageCheckerTrigger;
        }

        private async Task<BaseDocumentModel> OpenSVGFile(string fullPath)
        {
            SVGDocumentModel? d = new SVGDocumentModel(
                fullPath,
                Path.GetFileName(fullPath));
            await d.Init();
         
            return d;
        }


        private async Task<BaseDocumentModel> OpenImageFile(string fullPath)
        {
            ImageDocumentModel? d = new ImageDocumentModel(
                fullPath,
                Path.GetFileName(fullPath));
            await d.Init();
         

            return d;
        }

        private async Task<BaseDocumentModel> OpenJsonFile(string fileName, UMLUnknownDiagram diagram,
            int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fileName);
            JsonDocumentModel? d = new JsonDocumentModel((old, newdoc) =>
            {
            }, _ioService, diagram,
                _umlDocumentCollection, fileName, Path.GetFileName(fileName), content, _messageCheckerTrigger);

      
            d.GotoLineNumber(lineNumber, searchText);
            return d;
        }

        private async Task<BaseDocumentModel> OpenMarkDownFile(string fileName, int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fileName);
            MDDocumentModel? d = new MDDocumentModel(_ioService,
                fileName,
                Path.GetFileName(fileName)
                , content, _messageCheckerTrigger);

           
            d.GotoLineNumber(lineNumber, searchText);
            return d;
        }
      private async Task<BaseDocumentModel?> OpenURLLinkFile(string fileName)
        {
         

            string content = await File.ReadAllTextAsync(fileName);
            var url = content[(content.IndexOf("URL=", StringComparison.Ordinal) + 4)..];

            UrlLinkDocumentModel? d = new UrlLinkDocumentModel(
                fileName,
                Path.GetFileNameWithoutExtension(fileName),
                url);
 
    
            return d;
        }



        private async Task<BaseDocumentModel?> OpenTextFile(string fileName, int lineNumber, string? searchText)
        {
         

            string content = await File.ReadAllTextAsync(fileName);
            TextFileDocumentModel? d = new TextFileDocumentModel(_ioService,
                fileName,
                Path.GetFileName(fileName)
                , content, _messageCheckerTrigger);
 
            d.GotoLineNumber(lineNumber, searchText);
            return d;
        }

        private async Task<BaseDocumentModel> OpenUnknownDiagram(string fileName, UMLUnknownDiagram diagram)
        {
            string content = await File.ReadAllTextAsync(fileName);
            UnknownDocumentModel? d = new UnknownDocumentModel((old, @new) =>
            {
            }, _ioService, diagram, _umlDocumentCollection, fileName, Path.GetFileName(fileName), content, _messageCheckerTrigger);

          
            return d;
        }

        private async Task<BaseDocumentModel> OpenYMLFile(string fullPath, int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            YMLDocumentModel? d = new YMLDocumentModel(_ioService,
                fullPath,
                Path.GetFileName(fullPath)
                , content, _messageCheckerTrigger);

   
            d.GotoLineNumber(lineNumber, searchText);
            return  d;
        }
        private async Task<BaseDocumentModel> OpenSequenceDiagram(string fileName, UMLSequenceDiagram diagram,
    int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fileName);

            SequenceDiagramDocumentModel? d = new SequenceDiagramDocumentModel(
                _ioService, diagram, _umlDocumentCollection.ClassDocuments, fileName,
                Path.GetFileName(fileName), content, _messageCheckerTrigger);

       

            d.GotoLineNumber(lineNumber, searchText);

            return d;
        }
        private async Task<BaseDocumentModel> OpenComponentDiagram(string fileName, UMLComponentDiagram diagram,
int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fileName);
            ComponentDiagramDocumentModel? d = new ComponentDiagramDocumentModel(
                _ioService, diagram, fileName, Path.GetFileName(fileName), content, _messageCheckerTrigger);

        
            d.GotoLineNumber(lineNumber, searchText);

            return d;
        }

        public async Task<TextDocumentModel> OpenClassDiagram(string fileName,
        UMLClassDiagram diagram, int lineNumber, string? searchText = null)
        {
            string? content = await File.ReadAllTextAsync(fileName);
            ClassDiagramDocumentModel? d = new ClassDiagramDocumentModel(
                _ioService, diagram, _umlDocumentCollection.ClassDocuments, fileName, Path.GetFileName(fileName), content, _messageCheckerTrigger);

      
            d.GotoLineNumber(lineNumber, searchText);
 

            return d;
        }


        internal async Task<BaseDocumentModel?> TryOpen(string fileName, int lineNumber, string? searchText)
        {

            BaseDocumentModel? doc;
            lock (_docLock)
            {
                doc = _openDocuments.FirstOrDefault(p => p.FileName == fileName);
            }

            if (doc != null)
            {
                
                if (doc is TextDocumentModel textDocument)
                {
                    textDocument.GotoLineNumber(lineNumber, searchText);
                }

                return doc;
            }
            string ext = Path.GetExtension(fileName);

            if (FileExtension.PUML.Compare(ext))
            {
                (UMLClassDiagram? cd, UMLSequenceDiagram? sd, UMLComponentDiagram? comd, UMLUnknownDiagram? ud) =
                    await UMLDiagramTypeDiscovery.TryFindOrAddDocument(_umlDocumentCollection, fileName);

                if (cd != null)
                {
                    return await OpenClassDiagram(fileName, cd, lineNumber, searchText);
                }
                else if (sd != null)
                {
                    return await OpenSequenceDiagram(fileName, sd, lineNumber, searchText);
                }
                else if (comd != null)
                {
                    return await OpenComponentDiagram(fileName, comd, lineNumber, searchText);
                }
                else if (ud != null)
                {
                    foreach (var line in File.ReadLines(fileName))
                    {
                        if (line == "@startjson")
                        {
                            return await OpenJsonFile(fileName, ud, lineNumber, searchText);
                           
                        }
                    }

                    return await OpenUnknownDiagram(fileName, ud);
                }
            }
            else if (FileExtension.MD.Compare(ext))
            {
                return await OpenMarkDownFile(fileName, lineNumber, searchText);
            }
            else if (FileExtension.YML.Compare(ext))
            {
                return await OpenYMLFile(fileName, lineNumber, searchText);
            }
            else if (FileExtension.JPG.Compare(ext) || FileExtension.PNG.Compare(ext))
            {
                return await OpenImageFile(fileName);
            }
            else if(FileExtension.SVG.Compare(ext))
            {
                return await OpenSVGFile(fileName);
            }
            else if (FileExtension.URLLINK.Compare(ext))
            {
                return await OpenURLLinkFile(fileName);
            }
            else
            {
                return await OpenTextFile(fileName, lineNumber, searchText);
            }

            return null;
        }
    }
}
