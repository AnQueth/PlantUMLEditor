using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using System.Windows.Input;

using UMLModels;

namespace PlantUMLEditor.Models
{
    public class MainModel : BindingBase
    {
        private readonly IAutoComplete _autoComplete;
        private readonly IUMLDocumentCollectionSerialization _documentCollectionSerialization;
        private readonly Timer _messageChecker;
        private readonly IOpenDirectoryService _openDirectoryService;
        private object _docLock = new object();
        private Semaphore _fileSave = new Semaphore(1, 1);
        private string _folderBase;
        private string _metaDataDirectory = "";
        private string _metaDataFile = "";
        private DocumentModel currentDocument;

        private UMLModels.UMLDocumentCollection documents;

        private TreeViewModel folder;

        private DocumentMessage selectedMessage;

        public MainModel(IOpenDirectoryService openDirectoryService, IUMLDocumentCollectionSerialization documentCollectionSerialization, IAutoComplete autoComplete)
        {
            Documents = new UMLModels.UMLDocumentCollection();
            _messageChecker = new Timer(CheckMessages, null, 1000, Timeout.Infinite);
            _openDirectoryService = openDirectoryService;
            OpenDirectoryCommand = new DelegateCommand(OpenDirectoryHandler);
            SaveAllCommand = new DelegateCommand(SaveAllHandler);
            Folder = new TreeViewModel(Path.GetTempPath(), false, "");
            _documentCollectionSerialization = documentCollectionSerialization;
            OpenDocuments = new ObservableCollection<DocumentModel>();
            CreateNewSequenceDiagram = new DelegateCommand(NewSequenceDiagramHandler);
            CreateNewClassDiagram = new DelegateCommand(NewClassDiagramHandler);
            CloseDocument = new DelegateCommand<DocumentModel>(CloseDocumentHandler);
            CloseDocumentAndSave = new DelegateCommand<DocumentModel>(CloseDocumentAndSaveHandler);
            Messages = new ObservableCollection<DocumentMessage>();
            ScanAllFiles = new DelegateCommand(ScanAllFilesHandler);
            _autoComplete = autoComplete;

            Configuration = new AppConfiguration()
            {
                JarLocation = "d:\\downloads\\plantuml.jar"
            };
        }

        private AppConfiguration Configuration { get; }

        public DelegateCommand<DocumentModel> CloseDocument { get; }

        public DelegateCommand<DocumentModel> CloseDocumentAndSave { get; }

        public DelegateCommand CreateNewClassDiagram { get; }

        public DelegateCommand CreateNewSequenceDiagram { get; }

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

        public TreeViewModel Folder
        {
            get { return folder; }
            private set { SetValue(ref folder, value); }
        }

        public string JarLocation
        {
            get
            {
                return Configuration.JarLocation;
            }
            set
            {
                Configuration.JarLocation = value;
            }
        }

        public ObservableCollection<DocumentMessage> Messages
        {
            get;
        }

        public ICommand OpenDirectoryCommand
        {
            get;
        }

        public ObservableCollection<DocumentModel> OpenDocuments
        {
            get;
        }

        public ICommand SaveAllCommand
        {
            get;
        }

        public DelegateCommand ScanAllFiles { get; }

        public DocumentMessage SelectedMessage
        {
            get { return selectedMessage; }
            set
            {
                SetValue(ref selectedMessage, value);

                if (value != null)
                    AttemptOpeningFile(selectedMessage.FileName, selectedMessage.LineNumber);
            }
        }

        private void AddFolderItems(string dir, TreeViewModel model)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.puml"))
            {
                model.Children.Add(new TreeViewModel(file, true, "")
                {
                    Name = Path.GetFileName(file)
                });
            }

            foreach (var item in Directory.EnumerateDirectories(dir))
            {
                if (Path.GetFileName(item).StartsWith("."))
                    continue;

                var fm = new TreeViewModel(item, false, "")
                {
                    Name = Path.GetFileName(item)
                };
                model.Children.Add(fm);

                AddFolderItems(item, fm);
            }
        }

        private async Task AttemptOpeningFile(string fullPath, int lineNumber = 0)
        {
            var doc = OpenDocuments.FirstOrDefault(p => p.FileName == fullPath);

            if (doc != null)
            {
                CurrentDocument = doc;

                return;
            }

            _fileSave.WaitOne();
            UMLDiagramTypeDiscovery discovery = new UMLDiagramTypeDiscovery();
            var (cd, sd, ud) = await discovery.TryFindOrAddDocument(Documents, fullPath);
            _fileSave.Release();

            if (cd != null)
            {
                OpenClassDiagram(fullPath, cd, lineNumber);
            }
            else if (sd != null)
            {
                OpenSequenceDiagram(fullPath, sd, lineNumber);
            }
            else if (ud != null)
            {
                OpenUnknownDiagram(fullPath, ud);
            }
        }

        private async void CheckMessages(object state)
        {
            if (string.IsNullOrEmpty(_metaDataFile))
            {
                _messageChecker.Change(2000, Timeout.Infinite);
                return;
            }

            await this.SaveAll();

            if (Application.Current == null)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                DocumentMessageGenerator documentMessageGenerator = new DocumentMessageGenerator(Documents, Messages);
                documentMessageGenerator.Generate();

                _messageChecker.Change(2000, Timeout.Infinite);
            });
        }

        private void Close(DocumentModel doc)
        {
            doc.Close();
            lock (_docLock)
                OpenDocuments.Remove(doc);
        }

        private async void CloseDocumentAndSaveHandler(DocumentModel doc)
        {
            _fileSave.WaitOne();
            await Save(doc);

            _fileSave.Release();

            Close(doc);
        }

        private void CloseDocumentHandler(DocumentModel doc)
        {
            Close(doc);
        }

        private void DiagramModelChanged<T>(List<T> list, T old, T @new) where T : UMLDiagram
        {
            if (old != null && string.IsNullOrEmpty(@new.FileName))
            {
                @new.FileName = old.FileName;
            }

            list.RemoveAll(z => z.FileName == @new.FileName || z.Title == @new.Title);
            list.Add(@new);
        }

        private string GetNewFile(string fileExtension)
        {
            TreeViewModel selected = GetSelectedFolder(Folder);

            string dir = selected?.FullPath ?? _folderBase;

            if (string.IsNullOrEmpty(dir))
                return null;

            string nf = _openDirectoryService.NewFile(dir, fileExtension);
            return nf;
        }

        private TreeViewModel GetSelectedFile(TreeViewModel item)
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

        private TreeViewModel GetSelectedFolder(TreeViewModel item)
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

        private void NewClassDiagram(string fileName, string title)
        {
            var s = new UMLModels.UMLClassDiagram(title, fileName);

            var d = new ClassDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.ClassDocuments, old, @new), this._autoComplete, Configuration)
            {
                DocumentType = DocumentTypes.Class,
                Content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n",
                Diagram = s,
                FileName = fileName,
                Name = title
            };

            Documents.ClassDocuments.Add(s);

            lock (_docLock)
                OpenDocuments.Add(d);
            this.CurrentDocument = d;
        }

        private void NewClassDiagramHandler()
        {
            string nf = GetNewFile(".class.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                this.NewClassDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }

            this.ScanDirectory(this._folderBase);
        }

        private void NewSequenceDiagram(string fileName, string title)
        {
            var s = new UMLModels.UMLSequenceDiagram(title, fileName);

            var d = new SequenceDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.SequenceDiagrams, old, @new), this._autoComplete, Configuration)
            {
                DocumentType = DocumentTypes.Sequence,
                Content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n",
                Diagram = s,
                DataTypes = Documents.ClassDocuments,
                FileName = fileName,
                Name = title
            };

            Documents.SequenceDiagrams.Add(s);
            lock (_docLock)
                OpenDocuments.Add(d);
            this.CurrentDocument = d;
        }

        private void NewSequenceDiagramHandler()
        {
            string nf = GetNewFile(".seq.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                this.NewSequenceDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }

            this.ScanDirectory(this._folderBase);
        }

        private void OpenClassDiagram(string fileName, UMLClassDiagram diagram, int lineNumber)
        {
            var d = new ClassDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.ClassDocuments, old, @new), this._autoComplete, Configuration)
            {
                DocumentType = DocumentTypes.Class,
                Content = File.ReadAllText(fileName),
                Diagram = diagram,
                FileName = fileName,
                Name = diagram.Title
            };
            lock (_docLock)
                OpenDocuments.Add(d);

            d.GotoLineNumber(lineNumber);

            this.CurrentDocument = d;
        }

        private async void OpenDirectoryHandler()
        {
            _folderBase = null;
            await SaveAll();

            string dir = GetWorkingFolder();
            if (string.IsNullOrEmpty(dir))
                return;

            ScanDirectory(dir);
        }

        private void OpenSequenceDiagram(string fileName, UMLSequenceDiagram diagram, int lineNumber)
        {
            var d = new SequenceDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.SequenceDiagrams, old, @new), this._autoComplete, Configuration)
            {
                DataTypes = Documents.ClassDocuments,
                DocumentType = DocumentTypes.Sequence,

                Diagram = diagram,

                FileName = fileName,
                Name = diagram.Title,
                Content = File.ReadAllText(fileName)
            };
            lock (_docLock)
                OpenDocuments.Add(d);

            d.GotoLineNumber(lineNumber);

            this.CurrentDocument = d;
        }

        private void OpenUnknownDiagram(string fullPath, UMLUnknownDiagram diagram)
        {
            var d = new UnknownDocumentModel((old, @new) =>
            {
            }, this._autoComplete, Configuration)
            {
                DocumentType = DocumentTypes.Unknown,
                Diagrams = Documents,
                Diagram = diagram,

                FileName = fullPath,
                Name = diagram.Title,
                Content = File.ReadAllText(fullPath)
            };
            lock (_docLock)
                OpenDocuments.Add(d);
            this.CurrentDocument = d;
        }

        private async Task Save(DocumentModel doc)
        {
            if (doc.IsDirty)
            {
                await doc.PrepareSave();

                await File.WriteAllTextAsync(doc.FileName, doc.Content);
            }
        }

        private async Task SaveAll()
        {
            _fileSave.WaitOne();

            if (string.IsNullOrEmpty(_metaDataFile))
            {
                GetWorkingFolder();
            }

            if (string.IsNullOrEmpty(_metaDataFile))
            {
                return;
            }

            List<DocumentModel> c = new List<DocumentModel>();
            List<DocumentModel> s = new List<DocumentModel>();
            lock (_docLock)
            {
                c = OpenDocuments.Where(p => p is ClassDiagramDocumentModel).ToList();
                s = OpenDocuments.Where(p => p is SequenceDiagramDocumentModel).ToList();
            }

            foreach (var file in c)
            {
                await Save(file);
            }

            foreach (var file in s)
            {
                await Save(file);
            }

            await _documentCollectionSerialization.Save(Documents, _metaDataFile);

            _fileSave.Release();
        }

        private void SaveAllHandler()
        {
            SaveAll();
        }

        private async void ScanAllFilesHandler()
        {
            Documents.ClassDocuments.Clear();
            Documents.SequenceDiagrams.Clear();

            var folder = GetWorkingFolder();
            List<string> potentialSequenceDiagrams = new List<string>();
            await ScanForFiles(folder, potentialSequenceDiagrams);

            UMLDiagramTypeDiscovery discovery = new UMLDiagramTypeDiscovery();
            foreach (var seq in potentialSequenceDiagrams)
                await discovery.TryCreateSequenceDiagram(Documents, seq);
        }

        private void ScanDirectory(string dir)
        {
            Folder = new TreeViewModel("", false, "");

            Folder.Children.Clear();

            Folder.FullPath = dir;
            Folder.Name = Path.GetDirectoryName(dir);

            AddFolderItems(dir, Folder);

            ScanAllFilesHandler();
        }

        private async Task ScanForFiles(string folder, List<string> potentialSequenceDiagrams)
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.puml"))
            {
                UMLDiagramTypeDiscovery discovery = new UMLDiagramTypeDiscovery();
                if (null == await discovery.TryCreateClassDiagram(Documents, file))
                {
                    potentialSequenceDiagrams.Add(file);
                }
            }
            foreach (var file in Directory.EnumerateDirectories(folder))
            {
                await ScanForFiles(file, potentialSequenceDiagrams);
            }
        }

        public async void TreeItemClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                return;

            TreeViewModel fbm = GetSelectedFile(Folder);
            if (fbm == null)
                return;

            await AttemptOpeningFile(fbm.FullPath);
        }
    }
}