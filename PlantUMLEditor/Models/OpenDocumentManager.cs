﻿using PlantUMLEditor.Models.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUMLEditor.Models
{
    class OpenDocumentManager
    {
        private readonly ObservableCollection<BaseDocumentModel> _openDocuments;
        private readonly UMLDocumentCollection _umlDocumentCollection;
        private readonly object _docLock;
        private readonly IIOService _ioService;
        private readonly AutoResetEvent _messageCheckerTrigger;

        public OpenDocumentManager(ObservableCollection<BaseDocumentModel> openDocuments,
            UMLDocumentCollection umlDocumentCollection,
            object docLock, IIOService ioService, AutoResetEvent messageCheckerTrigger)
        {
            this._openDocuments = openDocuments;
            this._umlDocumentCollection = umlDocumentCollection;
            _docLock = docLock;
            _ioService = ioService;
            _messageCheckerTrigger = messageCheckerTrigger;
        }


        private async Task<BaseDocumentModel> OpenImageFile(string fullPath)
        {
            ImageDocumentModel? d = new ImageDocumentModel(
                fullPath,
                Path.GetFileName(fullPath));
            await d.Init();
         

            return d;
        }

        private async Task<BaseDocumentModel> OpenJsonFile(string fullPath, UMLUnknownDiagram diagram,
            int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            JsonDocumentModel? d = new JsonDocumentModel((old, newdoc) =>
            {
            }, _ioService, diagram,
                _umlDocumentCollection, fullPath, diagram.Title, content, _messageCheckerTrigger);

      
            d.GotoLineNumber(lineNumber, searchText);
            return d;
        }

        private async Task<BaseDocumentModel> OpenMarkDownFile(string fullPath, int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            MDDocumentModel? d = new MDDocumentModel(_ioService,
                fullPath,
                Path.GetFileName(fullPath)
                , content, _messageCheckerTrigger);

           
            d.GotoLineNumber(lineNumber, searchText);
            return d;
        }



        private async Task<BaseDocumentModel> OpenTextFile(string fullPath, int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            TextFileDocumentModel? d = new TextFileDocumentModel(_ioService,
                fullPath,
                Path.GetFileName(fullPath)
                , content, _messageCheckerTrigger);
 
            d.GotoLineNumber(lineNumber, searchText);
            return d;
        }

        private async Task<BaseDocumentModel> OpenUnknownDiagram(string fullPath, UMLUnknownDiagram diagram)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            UnknownDocumentModel? d = new UnknownDocumentModel((old, @new) =>
            {
            }, _ioService, diagram, _umlDocumentCollection, fullPath, diagram.Title, content, _messageCheckerTrigger);

          
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
                diagram.Title, content, _messageCheckerTrigger);

       

            d.GotoLineNumber(lineNumber, searchText);

            return d;
        }
        private async Task<BaseDocumentModel> OpenComponentDiagram(string fileName, UMLComponentDiagram diagram,
int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fileName);
            ComponentDiagramDocumentModel? d = new ComponentDiagramDocumentModel(
                _ioService, diagram, fileName, diagram.Title, content, _messageCheckerTrigger);

        
            d.GotoLineNumber(lineNumber, searchText);

            return d;
        }

        public async Task<TextDocumentModel> OpenClassDiagram(string fileName,
        UMLClassDiagram diagram, int lineNumber, string? searchText = null)
        {
            string? content = await File.ReadAllTextAsync(fileName);
            ClassDiagramDocumentModel? d = new ClassDiagramDocumentModel(
                _ioService, diagram, _umlDocumentCollection.ClassDocuments, fileName, diagram.Title, content, _messageCheckerTrigger);

      
            d.GotoLineNumber(lineNumber, searchText);
 

            return d;
        }


        internal async Task<BaseDocumentModel?> TryOpen(string fullPath, int lineNumber, string? searchText)
        {

            BaseDocumentModel? doc;
            lock (_docLock)
            {
                doc = _openDocuments.FirstOrDefault(p => p.FileName == fullPath);
            }

            if (doc != null)
            {
                
                if (doc is TextDocumentModel textDocument)
                {
                    textDocument.GotoLineNumber(lineNumber, searchText);
                }

                return doc;
            }
            string ext = Path.GetExtension(fullPath);

            if (FileExtension.PUML.Compare(ext))
            {
                (UMLClassDiagram? cd, UMLSequenceDiagram? sd, UMLComponentDiagram? comd, UMLUnknownDiagram? ud) =
                    await UMLDiagramTypeDiscovery.TryFindOrAddDocument(_umlDocumentCollection, fullPath);

                if (cd != null)
                {
                    return await OpenClassDiagram(fullPath, cd, lineNumber, searchText);
                }
                else if (sd != null)
                {
                    return await OpenSequenceDiagram(fullPath, sd, lineNumber, searchText);
                }
                else if (comd != null)
                {
                    return await OpenComponentDiagram(fullPath, comd, lineNumber, searchText);
                }
                else if (ud != null)
                {
                    foreach (var line in File.ReadLines(fullPath))
                    {
                        if (line == "@startjson")
                        {
                            return await OpenJsonFile(fullPath, ud, lineNumber, searchText);
                           
                        }
                    }

                    return await OpenUnknownDiagram(fullPath, ud);
                }
            }
            else if (FileExtension.MD.Compare(ext))
            {
                return await OpenMarkDownFile(fullPath, lineNumber, searchText);
            }
            else if (FileExtension.YML.Compare(ext))
            {
                return await OpenYMLFile(fullPath, lineNumber, searchText);
            }
            else if (FileExtension.JPG.Compare(ext) || FileExtension.PNG.Compare(ext))
            {
                return await OpenImageFile(fullPath);
            }
            else
            {
                return await OpenTextFile(fullPath, lineNumber, searchText);
            }

            return null;
        }
    }
}
