using Prism.Commands;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class MainModel : BindingBase
    {
        private string _metaDataFile = "";
        private string _metaDataDirectory = "";

        public DocumentModel CurrentDocument
        {
            get { return currentDocument; }
            set { SetValue(ref currentDocument, value); }
        }

        public UMLModels.UMLDocumentCollection Documents
        {
            get { return documents; }
            set { SetValue(ref documents, value); }
        }

        public FolderBindingModel Folder
        {
            get { return folder; }
            private set { SetValue(ref folder, value); }
        }

        public ObservableCollection<DocumentModel> OpenDocuments
        {
            get;
        }

        public DelegateCommand CreateNewSequenceDiagram { get; }
        public DelegateCommand CreateNewClassDiagram { get; }
        public DelegateCommand<DocumentModel> CloseDocument { get; }
        public DelegateCommand<DocumentModel> CloseDocumentAndSave { get; }

        private readonly IUMLDocumentCollectionSerialization _documentCollectionSerialization;

        public MainModel(IOpenDirectoryService openDirectoryService, IUMLDocumentCollectionSerialization documentCollectionSerialization)
        {
            Documents = new UMLModels.UMLDocumentCollection();
            _openDirectoryService = openDirectoryService;
            OpenDirectoryCommand = new DelegateCommand(OpenDirectory);
            SaveAllCommand = new DelegateCommand(() => SaveAll());
            Folder = new FolderBindingModel(Path.GetTempPath(), false);
            _documentCollectionSerialization = documentCollectionSerialization;
            OpenDocuments = new ObservableCollection<DocumentModel>();
            CreateNewSequenceDiagram = new DelegateCommand(NewSequenceDiagramHandler);
            CreateNewClassDiagram = new DelegateCommand(NewClassDiagramHandler);
            CloseDocument = new DelegateCommand<DocumentModel>(CloseDocumentHandler);
            CloseDocumentAndSave = new DelegateCommand<DocumentModel>(CloseDocumentAndSaveHandler);
        }

        private void CloseDocumentAndSaveHandler(DocumentModel doc)
        {
            Save(doc);

            OpenDocuments.Remove(doc);
        }

        private async Task Save(DocumentModel doc)
        {
            await doc.PrepareSave();

            await File.WriteAllTextAsync(doc.FileName, doc.Content);
        }

        private void CloseDocumentHandler(DocumentModel doc)
        {
            OpenDocuments.Remove(doc);
        }

        private void NewClassDiagramHandler()
        {
            string nf = GetNewFile();

            if (!string.IsNullOrEmpty(nf))
            {
                this.NewClassDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }

            this.OpenDirectory();
        }

        private FolderBindingModel GetSelectedFile(FolderBindingModel item)
        {
            if (item.IsSelected && item.IsFile)
                return item;

            foreach (var child in item.Children)
            {
                var f = GetSelectedFile(child);
                if (f != null)
                {
                    return f;
                }
            }

            return null;
        }

        private FolderBindingModel GetSelectedFolder(FolderBindingModel item)
        {
            if (item.IsSelected && !item.IsFile)
                return item;

            foreach (var child in item.Children)
            {
                var f = GetSelectedFolder(child);
                if (f != null)
                {
                    return f;
                }
            }

            return null;
        }

        public async void TreeItemClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                return;

            FolderBindingModel fbm = GetSelectedFile(Folder);
            if (fbm == null)
                return;

            if (OpenDocuments.Any(p => p.FileName == fbm.FullPath))
                return;

            var cd = Documents.ClassDocuments.Find(p => p.FileName == fbm.FullPath);
            var sd = Documents.SequenceDiagrams.Find(p => p.FileName == fbm.FullPath);
            if (cd == null &&  sd == null)
            {
                 sd = await PlantUML.UMLSequenceDiagramParser.ReadDiagram(fbm.FullPath, Documents.DataTypes, false);
                if (sd != null)
                {
                    Documents.SequenceDiagrams.Add(sd);
                    
                }
                else
                {
                    cd = await PlantUML.UMLClassDiagramParser.ReadClassDiagram(fbm.FullPath);
                    Documents.ClassDocuments.Add(cd);
                }
            }

            if (cd != null)
            {
                OpenClassDiagram(fbm.FullPath, cd);
            }
            else
            {
                OpenSequenceDiagram(fbm.FullPath, sd);
            }
        }

        private string GetNewFile()
        {
            FolderBindingModel selected = GetSelectedFolder(Folder);

            string dir = selected?.FullPath ?? _folderBase;

            if (string.IsNullOrEmpty(dir))
                return null;

            string nf = _openDirectoryService.NewFile(dir);
            return nf;
        }

        private void NewSequenceDiagramHandler()
        {
            string nf = GetNewFile();

            if (!string.IsNullOrEmpty(nf))
            {
                this.NewSequenceDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }

            this.OpenDirectory();
        }

        private void OpenSequenceDiagram(string fileName, UMLSequenceDiagram diagram)
        {
             
        


            var d = new SequenceDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.SequenceDiagrams, old, @new))
            {
                DataTypes = Documents.DataTypes,
                DocumentType = DocumentTypes.Sequence,

                Diagram = diagram,

                FileName = fileName,
                Name = diagram.Title,
                Content = File.ReadAllText(fileName)
            };

            OpenDocuments.Add(d);
            this.CurrentDocument = d;
        }

        private void OpenClassDiagram(string fileName, UMLClassDiagram diagram)
        {
             

            var d = new ClassDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.ClassDocuments, old, @new))
            {
                DocumentType = DocumentTypes.Class,
                Content = File.ReadAllText(fileName),
                Diagram = diagram,
                FileName = fileName,
                Name = diagram.Title
            };

            OpenDocuments.Add(d);
            this.CurrentDocument = d;
        }

        private void DiagramModelChanged<T>(List<T> list, T old, T @new) where T : UMLDiagram
        {
            if (old != null)
            {
                @new.FileName = old.FileName;
            }

            list.Remove(list.Find(z => z.FileName == @new.FileName || z.Title == @new.Title));
            list.Add(@new);
        }

        private void NewClassDiagram(string fileName, string title)
        {
            var s = new UMLModels.UMLClassDiagram(title, fileName);

            var d = new ClassDiagramDocumentModel((old, @new)=> DiagramModelChanged(Documents.ClassDocuments, old, @new))
            {
                DocumentType = DocumentTypes.Class,
                Content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n",
                Diagram = s,
                FileName = fileName,
                Name = title
            };

            Documents.ClassDocuments.Add(s);
            OpenDocuments.Add(d);
            this.CurrentDocument = d;
        }

        private void NewSequenceDiagram(string fileName, string title)
        {
            var s = new UMLModels.UMLSequenceDiagram(title, fileName);

            var d = new SequenceDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.SequenceDiagrams, old, @new))
            {
                DocumentType = DocumentTypes.Sequence,
                Content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n",
                Diagram = s,
                DataTypes = Documents.DataTypes,
                FileName = fileName,
                Name = title
            };

            Documents.SequenceDiagrams.Add(s);
            OpenDocuments.Add(d);
            this.CurrentDocument = d;
        }
 

        private async Task SaveAll()
        {
            if (string.IsNullOrEmpty(_metaDataFile))
            {
                GetWorkingFolder();
            }

            if (string.IsNullOrEmpty(_metaDataFile))
            {
                return;
            }

            foreach (var file in OpenDocuments)
            {
                await Save(file);
            }

            await _documentCollectionSerialization.Save(Documents, _metaDataFile);
        }

        private void OpenDirectory()
        {
            string dir = GetWorkingFolder();
            if (string.IsNullOrEmpty(dir))
                return;

            Folder = new FolderBindingModel("", false);

            Folder.Children.Clear();

            Folder.FullPath = dir;
            Folder.Name = Path.GetDirectoryName(dir);

            AddFolderItems(dir, Folder);

            _documentCollectionSerialization.Read(_metaDataFile).ContinueWith(p =>
            {
                Documents = p.Result;
            });
        }

        private string GetWorkingFolder()
        {
            if (string.IsNullOrEmpty(_folderBase))
            {
                string dir = _openDirectoryService.GetDirectory();
                if (string.IsNullOrEmpty(dir))
                    return null;

                _folderBase = dir;
            }

            _metaDataDirectory = Path.Combine(_folderBase, ".umlmetadata");

            if (!Directory.Exists(_metaDataDirectory))
            {
                Directory.CreateDirectory(_metaDataDirectory);
            }

            _metaDataFile = Path.Combine(_metaDataDirectory, "data.json");
            return _folderBase;
        }

        private void AddFolderItems(string dir, FolderBindingModel model)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.puml"))
            {
                model.Children.Add(new FolderBindingModel(file, true)
                {
                    Name = Path.GetFileName(file)
                });
            }

            foreach (var item in Directory.EnumerateDirectories(dir))
            {
                if (!item.StartsWith("."))
                    continue;

                var fm = new FolderBindingModel(item, false)
                {
                    Name = Path.GetFileName(item)
                };
                model.Children.Add(fm);

                AddFolderItems(item, fm);
            }
        }

        private readonly IOpenDirectoryService _openDirectoryService;
        private FolderBindingModel folder;
        private UMLModels.UMLDocumentCollection documents;
        private DocumentModel currentDocument;
        private string _folderBase;

        public ICommand OpenDirectoryCommand
        {
            get;
        }

        public ICommand SaveAllCommand
        {
            get;
        }
    }
}