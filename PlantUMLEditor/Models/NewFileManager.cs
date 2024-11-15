using System;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUMLEditor.Models
{

    internal class NewFileManager
    {
        private readonly IIOService _ioService;
        private readonly Func<TextDocumentModel, Task> _afterCreateCallback;
        private readonly UMLDocumentCollection _documentCollection;
        private readonly AutoResetEvent _messageCheckerTrigger;

        public NewFileManager(IIOService ioService,
            Func<TextDocumentModel, Task> afterCreateCallback,
            UMLDocumentCollection documentCollection,
            AutoResetEvent messageCheckerTrigger)
        {
            _ioService = ioService;
            _afterCreateCallback = afterCreateCallback;
            _documentCollection = documentCollection;
            _messageCheckerTrigger = messageCheckerTrigger;
        }
        private string? GetNewFile(TreeViewModel? selectedFolder, string? folderBase, string fileExtension)
        {
            if(selectedFolder == null && string.IsNullOrEmpty(folderBase))
            {
                return null;
            }

            string? dir = selectedFolder?.FullPath ?? folderBase;

            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }

            string? nf = _ioService.NewFile(dir, fileExtension);
            return nf;
        }
        public async Task CreateNewClassDiagram(TreeViewModel? selectedFolder, string? folderBase)
        {


            string? nf = GetNewFile(selectedFolder, folderBase, ".class.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewClassDiagram(nf, Path.GetFileNameWithoutExtension(nf));

                selectedFolder.Children.Insert(0, new TreeViewModel(selectedFolder, nf, Statics.GetIcon(nf)));
            }
        }
        public async Task CreateNewComponentDiagram(TreeViewModel? selectedFolder, string? folderBase)
        {


            string? nf = GetNewFile(selectedFolder, folderBase, ".component.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewComponentDiagram(nf, Path.GetFileNameWithoutExtension(nf));

                selectedFolder.Children.Insert(0, new TreeViewModel(selectedFolder, nf, Statics.GetIcon(nf)));
            }
        }
        private async Task NewClassDiagram(string fileName, string title)
        {
            UMLClassDiagram? model = new UMLModels.UMLClassDiagram(title, fileName, null);
            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";
            ClassDiagramDocumentModel? d = new ClassDiagramDocumentModel(
                  _ioService, model, _documentCollection.ClassDocuments, fileName,
                  title, content, _messageCheckerTrigger);

            _documentCollection.ClassDocuments.Add(model);

            await _afterCreateCallback(d);
        }

        private async Task NewComponentDiagram(string fileName, string title)
        {
            UMLComponentDiagram? model = new UMLModels.UMLComponentDiagram(title, fileName, null);

            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";
            ComponentDiagramDocumentModel? d = new ComponentDiagramDocumentModel(
                _ioService, model, fileName, title, content, _messageCheckerTrigger);

            _documentCollection.ComponentDiagrams.Add(model);
            await _afterCreateCallback(d);
        }
        private async Task NewJsonUMLDiagram(string fileName, string title, string content)
        {
            UMLUnknownDiagram? model = new UMLModels.UMLUnknownDiagram(title, fileName);

            JsonDocumentModel? d = new JsonDocumentModel((old, @new) =>
            {
            },
             _ioService, model, _documentCollection, fileName, title, content, _messageCheckerTrigger);

            await _afterCreateCallback(d);
        }

        public async Task CreateNewJsonDiagram(TreeViewModel? selectedFolder, string? folderBase)
        {
            string? nf = GetNewFile(selectedFolder, folderBase, ".json.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                string title = Path.GetFileNameWithoutExtension(nf);
                await NewJsonUMLDiagram(nf, title, $"@startjson\r\ntitle {title}\r\n\r\n@endjson\r\n");

                selectedFolder?.Children.Insert(0, new TreeViewModel(selectedFolder, nf, Statics.GetIcon(nf)));
            }
        }
        private async Task NewMarkDownDocument(string filePath, string fileName)
        {
            MDDocumentModel? d = new MDDocumentModel(
              _ioService, filePath, fileName, string.Empty, _messageCheckerTrigger);

            await _afterCreateCallback(d);
        }
        internal async Task CreateNewMarkdownFile(TreeViewModel? selectedFolder, string? folderBase)
        {
            string? nf = GetNewFile(selectedFolder, folderBase, ".md");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewMarkDownDocument(nf, Path.GetFileNameWithoutExtension(nf));

                selectedFolder?.Children.Insert(0, new TreeViewModel(selectedFolder, nf, Statics.GetIcon(nf)));
            }
        }
        private async Task NewSequenceDiagram(string fileName, string title)
        {
            UMLSequenceDiagram? model = new UMLModels.UMLSequenceDiagram(title, fileName);
            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";

            SequenceDiagramDocumentModel? d = new SequenceDiagramDocumentModel(
                _ioService, model, _documentCollection.ClassDocuments, fileName, title, content, _messageCheckerTrigger);

            _documentCollection.SequenceDiagrams.Add(model);
            await _afterCreateCallback(d);
        }
        internal async Task CreateNewSequenceFile(TreeViewModel? selectedFolder, string? folderBase)
        {

            string? nf = GetNewFile(selectedFolder, folderBase, ".seq.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewSequenceDiagram(nf, Path.GetFileNameWithoutExtension(nf));

                selectedFolder?.Children.Insert(0, new TreeViewModel(selectedFolder, nf, Statics.GetIcon(nf)));
            }
        }

        private async Task NewUnknownUMLDiagram(string fileName, string title, string content)
        {
            UMLUnknownDiagram? model = new UMLModels.UMLUnknownDiagram(title, fileName);

            UnknownDocumentModel? d = new UnknownDocumentModel((old, @new) =>
            {
            },
              _ioService, model, _documentCollection, fileName, title, content, _messageCheckerTrigger);

            await _afterCreateCallback(d);
        }

        internal async Task CreateNewUnknownDiagramFile(TreeViewModel? selectedFolder, string? folderBase)
        {
            string? nf = GetNewFile(selectedFolder, folderBase, ".puml");

            if (!string.IsNullOrEmpty(nf))
            {
                string title = Path.GetFileNameWithoutExtension(nf);
                await NewUnknownUMLDiagram(nf, title, $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n");

                selectedFolder?.Children.Insert(0, new TreeViewModel(selectedFolder,
                    nf, Statics.GetIcon(nf)));
            }
        }
        private async Task NewYAMLDocument(string filePath, string fileName)
        {
            YMLDocumentModel? d = new YMLDocumentModel(
             _ioService, filePath, fileName, string.Empty, _messageCheckerTrigger);

            await _afterCreateCallback(d);
        }
        internal async Task CreateNewYamlFile(TreeViewModel? selectedFolder, string? folderBase)
        {
            string? nf = GetNewFile(selectedFolder, folderBase, ".yml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewYAMLDocument(nf, Path.GetFileNameWithoutExtension(nf));

                selectedFolder?.Children.Insert(0, new TreeViewModel(selectedFolder, nf, Statics.GetIcon(nf)));
            }
        }

    }
}