using Prism.Commands;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class MainModel : BindingBase
    {
        private string _metaDataFile = "";
        private string _metaDataDirectory = "";

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
            FolderBindingModel selected = GetSelectedFolder(Folder);

            string dir = selected?.FullPath;
            if (string.IsNullOrEmpty(dir))
                return;
            string nf = _openDirectoryService.NewFile(dir);
            if (!string.IsNullOrEmpty(nf))
            {
                this.NewClassDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }
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

        public void TreeItemClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                return;

            FolderBindingModel fbm = GetSelectedFile(Folder);
            if (fbm == null)
                return;

            var d = Documents.ClassDocuments.Find(p => p.FileName == fbm.FullPath);

            if (d != null)
            {
                OpenClassDiagram(fbm.FullPath);
            }
            else
            {
                OpenSequenceDiagram(fbm.FullPath);
            }
        }

        private void NewSequenceDiagramHandler()
        {
            FolderBindingModel selected = GetSelectedFolder(Folder);
            string dir = selected?.FullPath;

            if (string.IsNullOrEmpty(dir))
                return;

            string nf = _openDirectoryService.NewFile(dir);
            if (!string.IsNullOrEmpty(nf))
            {
                this.NewSequenceDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }
        }

        private void OpenSequenceDiagram(string fileName)
        {
            var diagram = this.Documents.SequenceDiagrams.Find(p => p.FileName == fileName);

            var d = new SequenceDiagramDocumentModel(SequenceDiagramModelChanged)
            {
                DataTypes = Documents.DataTypes,
                DocumentType = DocumentTypes.Sequence,

                Diagram = diagram,

                FileName = fileName,
                Name = diagram.Title,
                Content = File.ReadAllText(fileName)
            };

            OpenDocuments.Add(d);
        }

        private void OpenClassDiagram(string fileName)
        {
            var diagram = this.Documents.ClassDocuments.Find(p => p.FileName == fileName);

            var d = new ClassDiagramDocumentModel(ClassDiagramModelChanged)
            {
                DocumentType = DocumentTypes.Class,
                Content = File.ReadAllText(fileName),
                Diagram = diagram,
                FileName = fileName,
                Name = diagram.Title
            };

            OpenDocuments.Add(d);
        }

        private void ClassDiagramModelChanged(UMLClassDiagram old, UMLClassDiagram @new)
        {
            if (old != null)
            {
                @new.FileName = old.FileName;
            }

            Documents.ClassDocuments.Remove(Documents.ClassDocuments.Find(z => z.FileName == @new.FileName));
            Documents.ClassDocuments.Add(@new);
        }

        private void NewClassDiagram(string fileName, string title)
        {
            var s = new UMLModels.UMLClassDiagram(title, fileName);

            var d = new ClassDiagramDocumentModel(ClassDiagramModelChanged)
            {
                DocumentType = DocumentTypes.Class,
                Content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n",
                Diagram = s,
                FileName = fileName,
                Name = title
            };

            Documents.ClassDocuments.Add(s);
            OpenDocuments.Add(d);
        }

        private void NewSequenceDiagram(string fileName, string title)
        {
            var s = new UMLModels.UMLSequenceDiagram(title, fileName);

            var d = new SequenceDiagramDocumentModel(SequenceDiagramModelChanged)
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
        }

        private void SequenceDiagramModelChanged(UMLSequenceDiagram old, UMLSequenceDiagram @new)
        {
            @new.FileName = old.FileName;

            Documents.SequenceDiagrams.Remove(old);
            Documents.SequenceDiagrams.Add(@new);
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
            string dir = _openDirectoryService.GetDirectory();
            if (string.IsNullOrEmpty(dir))
                return null;

            _metaDataDirectory = Path.Combine(dir, ".umlmetadata");

            if (!Directory.Exists(_metaDataDirectory))
            {
                Directory.CreateDirectory(_metaDataDirectory);
            }

            _metaDataFile = Path.Combine(_metaDataDirectory, "data.json");
            return dir;
        }

        private void AddFolderItems(string dir, FolderBindingModel model)
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                model.Children.Add(new FolderBindingModel(file, true)
                {
                    Name = file
                });
            }

            foreach (var item in Directory.EnumerateDirectories(dir))
            {
                var fm = new FolderBindingModel(item, false)
                {
                    Name = item
                };
                model.Children.Add(fm);

                AddFolderItems(item, fm);
            }
        }

        private readonly IOpenDirectoryService _openDirectoryService;
        private FolderBindingModel folder;
        private UMLModels.UMLDocumentCollection documents;

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