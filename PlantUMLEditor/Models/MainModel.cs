using PlantUML;
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
    public class MainModel : BindingBase, IFolderChangeNotifactions
    {
        private readonly ObservableCollection<string> _dataTypes = new ObservableCollection<string>();
        private readonly IUMLDocumentCollectionSerialization _documentCollectionSerialization;
        private readonly Timer _messageChecker;
        private readonly IOpenDirectoryService _openDirectoryService;
        private bool _AllowContinue;
        private bool _confirmOpen;
        private object _docLock = new object();
        private Semaphore _fileSave = new Semaphore(1, 1);
        private string _folderBase;
        private string _metaDataDirectory = "";
        private string _metaDataFile = "";
        private DocumentModel currentDocument;
        private UMLModels.UMLDocumentCollection documents;
        private TreeViewModel folder;
        private DocumentMessage selectedMessage;

        public MainModel(IOpenDirectoryService openDirectoryService, IUMLDocumentCollectionSerialization documentCollectionSerialization)
        {
            Documents = new UMLModels.UMLDocumentCollection();
            _messageChecker = new Timer(CheckMessages, null, 1000, Timeout.Infinite);
            _openDirectoryService = openDirectoryService;
            OpenDirectoryCommand = new DelegateCommand(OpenDirectoryHandler);
            SaveAllCommand = new DelegateCommand(SaveAllHandler);
            Folder = new TreeViewModel(Path.GetTempPath(), false, "", this);
            _documentCollectionSerialization = documentCollectionSerialization;
            OpenDocuments = new ObservableCollection<DocumentModel>();
            CreateNewSequenceDiagram = new DelegateCommand(NewSequenceDiagramHandler);
            CreateNewClassDiagram = new DelegateCommand(NewClassDiagramHandler);
            CloseDocument = new DelegateCommand<DocumentModel>(CloseDocumentHandler);
            CloseDocumentAndSave = new DelegateCommand<DocumentModel>(CloseDocumentAndSaveHandler);
            SaveCommand = new DelegateCommand<DocumentModel>(SaveCommandHandler);
            Messages = new ObservableCollection<DocumentMessage>();
            SelectDocumentCommand = new DelegateCommand<DocumentModel>(SelectDocumentHandler);

            ScanAllFiles = new DelegateCommand(ScanAllFilesHandler);

            Configuration = new AppConfiguration()
            {
                JarLocation = "plantuml.jar"
            };
        }

        private AppConfiguration Configuration { get; }

        public bool AllowContinue
        {
            get
            {
                return _AllowContinue;
            }
            set
            {
                SetValue(ref _AllowContinue, value);
            }
        }

        public DelegateCommand<DocumentModel> CloseDocument { get; }

        public DelegateCommand<DocumentModel> CloseDocumentAndSave { get; }

        public bool ConfirmOpen
        {
            get
            {
                return _confirmOpen;
            }
            set
            {
                SetValue(ref _confirmOpen, value);
            }
        }

        public DelegateCommand CreateNewClassDiagram { get; }

        public DelegateCommand CreateNewSequenceDiagram { get; }

        public DocumentModel CurrentDocument
        {
            get { return currentDocument; }
            set
            {
                if (currentDocument != null)
                    currentDocument.Visible = Visibility.Collapsed;

                SetValue(ref currentDocument, value);
                if (value != null)
                    value.Visible = Visibility.Visible;
            }
        }

        public ObservableCollection<string> DataTypes
        {
            get
            {
                return _dataTypes;
            }
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

        public DelegateCommand<DocumentModel> SaveCommand { get; }

        public DelegateCommand ScanAllFiles { get; }

        public ICommand SelectDocumentCommand { get; }

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
                model.Children.Add(new TreeViewModel(file, true, !file.Contains(".seq.puml") ? "images\\class.png" : "images\\sequence.png", this)
                {
                });
            }

            foreach (var item in Directory.EnumerateDirectories(dir))
            {
                if (Path.GetFileName(item).StartsWith("."))
                    continue;

                var fm = new TreeViewModel(item, false, "", this)
                {
                };
                model.Children.Add(fm);

                AddFolderItems(item, fm);
            }
        }

        private void AddMethodToClassDigramHandler(DocumentMessage sender)
        {
            foreach (var doc in Documents.ClassDocuments)
            {
                var d = doc.DataTypes.FirstOrDefault(p => p.Id == sender.DataTypeId);
                if (d == null)
                    continue;

                UMLClassDiagramParser.TryParseLineForDataType(sender.OffendingText.Trim(), new Dictionary<string, UMLDataType>(), d);

                var od = OpenDocuments.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => p.FileName == doc.FileName);
                if (od != null)
                {
                    CurrentDocument = od;
                    od.UpdateDiagram(doc);
                }
                else
                {
                    OpenClassDiagram(doc.FileName, doc, 0);
                }
            }
        }

        private async Task AttemptOpeningFile(string fullPath, int lineNumber = 0)
        {
            var doc = OpenDocuments.FirstOrDefault(p => p.FileName == fullPath);

            if (doc != null)
            {
                CurrentDocument = doc;
                doc.GotoLineNumber(lineNumber);
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

            if (Application.Current == null)
                return;
            List<UMLDiagram> diagrams = new List<UMLDiagram>();

            try
            {
                foreach (var doc in OpenDocuments)
                {
                    var d = await doc.GetEditedDiagram();
                    d.FileName = doc.FileName;
                    diagrams.Add(d);
                }
            }
            catch
            {
                _messageChecker.Change(2000, Timeout.Infinite);
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
          {
              Messages.Clear();

              DocumentMessageGenerator documentMessageGenerator = new DocumentMessageGenerator(diagrams, Messages);
              documentMessageGenerator.Generate();

              foreach (var d in Messages)
              {
                  d.CreateMissingMethodCommand = new DelegateCommand<DocumentMessage>(AddMethodToClassDigramHandler);

                  var docs = OpenDocuments.Where(p => p.FileName == d.FileName);
                  foreach (var doc in docs)
                  {
                      if (CurrentDocument == doc)
                          doc.ReportMessage(d);
                  }
              }

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
            if (doc.IsDirty)
            {
            }
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

            var d = new ClassDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.ClassDocuments, old, @new), Configuration)
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

            var d = new SequenceDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.SequenceDiagrams, old, @new), Configuration)
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
            var d = new ClassDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.ClassDocuments, old, @new), Configuration)
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
            var d = new SequenceDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.SequenceDiagrams, old, @new), Configuration)
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
            }, Configuration)
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
            await File.WriteAllTextAsync(doc.FileName, doc.Content);
            doc.IsDirty = false;
        }

        private async Task SaveAll()
        {
            _fileSave.WaitOne();
            try
            {
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

                List<(UMLDiagram, UMLDiagram)> list = new List<(UMLDiagram, UMLDiagram)>();

                foreach (var document in OpenDocuments.OfType<ClassDiagramDocumentModel>())
                {
                    var e = await document.GetEditedDiagram();
                    e.FileName = document.FileName;

                    if (e is UMLClassDiagram cd)
                    {
                        foreach (var oldCd in Documents.ClassDocuments)
                        {
                            if (oldCd.FileName == cd.FileName)
                            {
                                list.Add((oldCd, cd));
                            }
                        }
                    }
                }
                foreach (var document in OpenDocuments.OfType<SequenceDiagramDocumentModel>())
                {
                    var e = await document.GetEditedDiagram();
                    e.FileName = document.FileName;

                    if (e is UMLSequenceDiagram sd)
                    {
                        foreach (var oldCd in Documents.SequenceDiagrams)
                        {
                            if (oldCd.FileName == sd.FileName)
                            {
                                list.Add((oldCd, sd));
                            }
                        }
                    }
                }

                foreach (var item in list)
                {
                    if (item.Item1 is UMLClassDiagram cd)
                    {
                        documents.ClassDocuments.Remove(cd);
                        documents.ClassDocuments.Add(item.Item2 as UMLClassDiagram);
                    }
                    else
                    {
                        documents.SequenceDiagrams.Remove(item.Item1 as UMLSequenceDiagram);
                        documents.SequenceDiagrams.Add(item.Item2 as UMLSequenceDiagram);
                    }
                }

                foreach (var document in OpenDocuments.OfType<SequenceDiagramDocumentModel>())
                {
                    document.UpdateDiagram(documents.ClassDocuments);
                }

                await _documentCollectionSerialization.Save(Documents, _metaDataFile);
            }
            finally
            {
                _fileSave.Release();
            }
        }

        private void SaveAllHandler()
        {
            SaveAll();
        }

        private async void SaveCommandHandler(DocumentModel doc)
        {
            _fileSave.WaitOne();
            await Save(doc);

            _fileSave.Release();
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

            foreach (var doc in OpenDocuments.OfType<SequenceDiagramDocumentModel>())
            {
                doc.UpdateDiagram(Documents.ClassDocuments);
            }
        }

        private void ScanDirectory(string dir)
        {
            Folder = new TreeViewModel("", false, "", this);

            Folder.Children.Clear();

            var start = new TreeViewModel(dir, false, "", this);

            Folder.Children.Add(start);

            AddFolderItems(dir, start);

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

        private void SelectDocumentHandler(DocumentModel model)
        {
            CurrentDocument = model;
        }

        public void Change(string fullPath)
        {
            string dir = GetWorkingFolder();
            if (string.IsNullOrEmpty(dir))
                return;

            ScanDirectory(dir);
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