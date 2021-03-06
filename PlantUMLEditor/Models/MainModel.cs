﻿using Newtonsoft.Json;
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
using System.Windows.Controls;
using System.Windows.Input;

using UMLModels;

namespace PlantUMLEditor.Models
{
    public class MainModel : BindingBase, IFolderChangeNotifactions
    {
        private readonly ObservableCollection<Tuple<string, UMLDataType>> _dataTypes = new ObservableCollection<Tuple<string, UMLDataType>>();
        private readonly IUMLDocumentCollectionSerialization _documentCollectionSerialization;
        private readonly IIOService _ioService;
        private readonly Timer _messageChecker;

        internal bool CloseAll()
        {
            foreach(var item in OpenDocuments)
            {
                if (item.IsDirty)
                    return true;

               item.TryClosePreview();
            }

            return false;
        }

        private bool _AllowContinue;
        private bool _confirmOpen;
        private object _docLock = new object();
        private Semaphore _fileSave = new Semaphore(1, 1);
        private string _folderBase;
        private DelegateCommand<string> _gotoDefinitionCommand;
        private string _metaDataDirectory = "";
        private string _metaDataFile = "";
        private GlobalFindResult _selectedFindResult;
        private DocumentModel currentDocument;
        private UMLModels.UMLDocumentCollection documents;
        private TreeViewModel folder;
        private DocumentMessage selectedMessage;

        public MainModel(IIOService openDirectoryService, IUMLDocumentCollectionSerialization documentCollectionSerialization)
        {
            _gotoDefinitionCommand = new DelegateCommand<string>(GotoDefinitionInvoked);
            Documents = new UMLModels.UMLDocumentCollection();
            _messageChecker = new Timer(CheckMessages, null, 1000, Timeout.Infinite);
            _ioService = openDirectoryService;
            OpenDirectoryCommand = new DelegateCommand(() => OpenDirectoryHandler());

            SaveAllCommand = new DelegateCommand(SaveAllHandler, () => !string.IsNullOrEmpty(_folderBase));
            Folder = new TreeViewModel(null, Path.GetTempPath(), false, "", this);
            _documentCollectionSerialization = documentCollectionSerialization;
            OpenDocuments = new ObservableCollection<DocumentModel>();
            CreateNewUnknownDiagram = new DelegateCommand(NewUnknownDiagramHandler, () => !string.IsNullOrEmpty(_folderBase));

            CreateNewSequenceDiagram = new DelegateCommand(NewSequenceDiagramHandler, () => !string.IsNullOrEmpty(_folderBase));
            CreateNewClassDiagram = new DelegateCommand(NewClassDiagramHandler, () => !string.IsNullOrEmpty(_folderBase));
            CreateNewComponentDiagram = new DelegateCommand(NewComponentDiagramHandler, () => !string.IsNullOrEmpty(_folderBase));
            CloseDocument = new DelegateCommand<DocumentModel>(CloseDocumentHandler);
            CloseDocumentAndSave = new DelegateCommand<DocumentModel>(CloseDocumentAndSaveHandler);
            SaveCommand = new DelegateCommand<DocumentModel>(SaveCommandHandler);
            Messages = new ObservableCollection<DocumentMessage>();
            SelectDocumentCommand = new DelegateCommand<DocumentModel>(SelectDocumentHandler);
            GlobalSearchCommand = new DelegateCommand<string>(GlobalSearchHandler);
            ScanAllFiles = new DelegateCommand(() => ScanAllFilesHandler(), () => !string.IsNullOrEmpty(_folderBase));

            Configuration = new AppConfiguration()
            {
                JarLocation = "plantuml.jar"
            };

            OpenDocuments.CollectionChanged += OpenDocuments_CollectionChanged;
            GridSettings = !string.IsNullOrEmpty(AppSettings.Default.GridSettings) ?
                JsonConvert.DeserializeObject<GridSettings>(AppSettings.Default.GridSettings) : new GridSettings(GridSettingsChanged);
            GridSettings.ChangedCB = GridSettingsChanged;
            WindowWidth = AppSettings.Default.WindowWidth;
            WindowHeight = AppSettings.Default.WindowHeight;
            WindowTop = AppSettings.Default.WindowTop;
            WindowLeft = AppSettings.Default.WindowLeft;

            var mrus = JsonConvert.DeserializeObject<string[]>(AppSettings.Default.MRU ?? "[]");
            if (mrus != null)
                foreach (var s in mrus)
                {
                    MRUFolders.Add(s);
                }

        }



        public int WindowLeft { get; set; }
        public int WindowTop { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }

        public void UISizeChanged()
        {
            AppSettings.Default.WindowWidth = WindowWidth;
            AppSettings.Default.WindowHeight = WindowHeight;
            AppSettings.Default.WindowTop = WindowTop;
            AppSettings.Default.WindowLeft = WindowLeft;
            AppSettings.Default.Save();

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

        public DelegateCommand CreateNewComponentDiagram { get; }

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

        public ObservableCollection<Tuple<string, UMLDataType>> DataTypes
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

        public ObservableCollection<string> MRUFolders { get; } = new ObservableCollection<string>();
        public ObservableCollection<GlobalFindResult> GlobalFindResults { get; } = new ObservableCollection<GlobalFindResult>();

        public DelegateCommand<string> GlobalSearchCommand { get; }

        public DelegateCommand<string> GotoDefinitionCommand
        {
            get => _gotoDefinitionCommand;
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


        public GridSettings GridSettings
        {
            get;
            private set;
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
        public DelegateCommand CreateNewUnknownDiagram { get; }
        public DelegateCommand SaveAllCommand
        {
            get;
        }

        public DelegateCommand<DocumentModel> SaveCommand { get; }

        public DelegateCommand ScanAllFiles { get; }

        public DelegateCommand<DocumentModel> SelectDocumentCommand { get; }

        public GlobalFindResult SelectedGlobalFindResult
        {
            get
            {
                return _selectedFindResult;
            }
            set
            {
                _selectedFindResult = value;
                if (value != null)
                    this.AttemptOpeningFile(value.FileName,
                        value.LineNumber, value.SearchText);
            }
        }

        public DocumentMessage SelectedMessage
        {
            get { return selectedMessage; }
            set
            {
                SetValue(ref selectedMessage, value);

                if (value != null && !string.IsNullOrEmpty(selectedMessage.FileName))
                    AttemptOpeningFile(selectedMessage.FileName, selectedMessage.LineNumber);
            }
        }

        private void AddFolderItems(string dir, TreeViewModel model)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.puml"))
            {
                model.Children.Add(new TreeViewModel(model, file, true, !file.Contains(".component.puml") ?
                    (!file.Contains(".seq.puml") ? "images\\class.png" : "images\\sequence.png") : "images\\com.png", this)
                {
                });
            }

            foreach (var item in Directory.EnumerateDirectories(dir))
            {
                if (Path.GetFileName(item).StartsWith("."))
                    continue;

                var fm = new TreeViewModel(model, item, false, "", this)
                {
                };
                model.Children.Add(fm);

                AddFolderItems(item, fm);
            }
        }

        private void AfterCreate(DocumentModel d)
        {
            lock (_docLock)
                OpenDocuments.Add(d);

            d.Save();

            this.CurrentDocument = d;
        }

        private async Task AttemptOpeningFile(string fullPath,
            int lineNumber = 0, string searchText = null)
        {
            var doc = OpenDocuments.FirstOrDefault(p => p.FileName == fullPath);

            if (doc != null)
            {
                CurrentDocument = doc;
                doc.GotoLineNumber(lineNumber, searchText);
                return;
            }

            _fileSave.WaitOne();
            UMLDiagramTypeDiscovery discovery = new UMLDiagramTypeDiscovery();
            var (cd, sd, comd, ud) = await discovery.TryFindOrAddDocument(Documents, fullPath);
            _fileSave.Release();

            if (cd != null)
            {
                OpenClassDiagram(fullPath, cd, lineNumber, searchText);
            }
            else if (sd != null)
            {
                OpenSequenceDiagram(fullPath, sd, lineNumber, searchText);
            }
            else if (comd != null)
                OpenComponentDiagram(fullPath, comd, lineNumber, searchText);
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

                foreach (var doc in Documents.ClassDocuments)
                {
                    if (!OpenDocuments.Any(p => p.FileName == doc.FileName))
                        diagrams.Add(doc);
                }
                foreach (var doc in Documents.ComponentDiagrams)
                {
                    if (!OpenDocuments.Any(p => p.FileName == doc.FileName))
                        diagrams.Add(doc);
                }
                foreach (var doc in Documents.SequenceDiagrams)
                {
                    if (!OpenDocuments.Any(p => p.FileName == doc.FileName))
                        diagrams.Add(doc);
                }
            }
            catch
            {
                _messageChecker.Change(2000, Timeout.Infinite);
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
          {
              //Messages.Clear();

              DocumentMessageGenerator documentMessageGenerator = new DocumentMessageGenerator(diagrams, Messages);
              documentMessageGenerator.Generate(_folderBase);

              foreach (var d in Messages)
              {
                  if (d.IsMissingMethod || d.IsMissingDataType)
                      d.FixingCommand = new DelegateCommand<DocumentMessage>(FixingCommandHandler);

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

            CurrentDocument = OpenDocuments.LastOrDefault();
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

        private void FixingCommandHandler(DocumentMessage sender)
        {
            if (sender.IsMissingMethod)
            {
                foreach (var doc in Documents.ClassDocuments)
                {
                    var d = doc.DataTypes.FirstOrDefault(p => p.Id == sender.MissingMethodDataTypeId);
                    if (d == null)
                        continue;

                    UMLClassDiagramParser.TryParseLineForDataType(sender.MissingMethodText.Trim(), new Dictionary<string, UMLDataType>(), d);

                    var od = OpenDocuments.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => p.FileName == doc.FileName);
                    if (od != null)
                    {
                        CurrentDocument = od;
                        od.UpdateDiagram(doc);
                    }
                    else
                    {
                        OpenClassDiagram(doc.FileName, doc, 0, null);
                    }
                }
            }
            else if (sender.IsMissingDataType)
            {
                var f = Documents.ClassDocuments.FirstOrDefault(p => p.Title == "defaults.class");
                if (f != null)
                {
                    if (f.Package == null)
                        f.Package = new UMLPackage("defaults");

                    f.Package.Children.Add(new UMLClass("default", false, sender.MissingDataTypeName, new List<UMLDataType>()));
                    string d = Path.Combine(GetWorkingFolder(true), "defaults.class.puml");

                    var od = OpenDocuments.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => p.FileName == d);
                    if (od != null)
                    {
                        CurrentDocument = od;
                        od.UpdateDiagram(f);
                    }
                    else
                    {
                        OpenClassDiagram(d, f, 0, null);

                        od = OpenDocuments.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => p.FileName == d);
                        if (od != null)
                        {
                            od.UpdateDiagram(f);
                        }

                    }
                }
                else
                {
                    MessageBox.Show("Create a defaults.class document in the root of the work folder first.");
                }
            }
        }

        private string GetNewFile(string fileExtension)
        {
            TreeViewModel selected = GetSelectedFolder(Folder);

            string dir = selected?.FullPath ?? _folderBase;

            if (string.IsNullOrEmpty(dir))
                return null;

            string nf = _ioService.NewFile(dir, fileExtension);
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

        private string GetWorkingFolder(bool useAppSettingIfFound = false, string folder = null)
        {
            if (useAppSettingIfFound)
            {
                _folderBase = AppSettings.Default.WorkingDir;
            }

            if (string.IsNullOrEmpty(_folderBase))
            {
                string dir = folder;
                if (folder == null)
                {
                      dir = _ioService.GetDirectory();
                    if (string.IsNullOrEmpty(dir))
                        return null;
                }

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

        private async void GlobalSearchHandler(string obj)
        {
            GlobalSearch gs = new GlobalSearch();
            var findresults = await gs.Find(_folderBase, obj, new string[]
            {"*.puml"
            });
            GlobalFindResults.Clear();
            foreach (var f in findresults)
                GlobalFindResults.Add(f);
        }

        private void GotoDefinitionInvoked(string text)
        {
            foreach (var item in documents.ClassDocuments.Where(z => z.DataTypes.Any(v => v.Name == text)).Select(p => new
            {
                FN = p.FileName,
                D = p,
                DT = p.DataTypes.First(z => z.Name == text)
            }))
            {
                OpenClassDiagram(item.FN, item.D, item.DT.LineNumber, null);
            }
        }

        private void NewClassDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLClassDiagram(title, fileName);

            var d = new ClassDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.ClassDocuments, old, @new),
                Configuration, _ioService)
            {
                DocumentType = DocumentTypes.Class,
                Content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n",
                Diagram = model,
                FileName = fileName,
                Name = title
            };

            Documents.ClassDocuments.Add(model);

            AfterCreate(d);
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

        private void NewComponentDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLComponentDiagram(title, fileName);

            var d = new ComponentDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.ComponentDiagrams, old, @new), Configuration, _ioService)
            {
                DocumentType = DocumentTypes.Component,
                Content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n",
                Diagram = model,
                FileName = fileName,
                Name = title
            };

            Documents.ComponentDiagrams.Add(model);
            AfterCreate(d);
        }

        private void NewComponentDiagramHandler()
        {
            string nf = GetNewFile(".component.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                this.NewComponentDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }

            this.ScanDirectory(this._folderBase);
        }

        private void NewSequenceDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLSequenceDiagram(title, fileName);

            var d = new SequenceDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.SequenceDiagrams, old, @new), Configuration, _ioService)
            {
                DocumentType = DocumentTypes.Sequence,
                Content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n",
                Diagram = model,
                DataTypes = Documents.ClassDocuments,
                FileName = fileName,
                Name = title
            };

            Documents.SequenceDiagrams.Add(model);
            AfterCreate(d);
        }
        private void NewUnknownDiagramHandler()
        {
            string nf = GetNewFile(".puml");

            if (!string.IsNullOrEmpty(nf))
            {
                this.NewUnknownUMLDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }

            this.ScanDirectory(this._folderBase);
        }

        private void NewUnknownUMLDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLUnknownDiagram(title, fileName);

            var d = new UnknownDocumentModel((old, @new) =>
            { },
            Configuration, _ioService)
            {
                DocumentType = DocumentTypes.Unknown,
                Content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n",
                Diagram = model,
               Diagrams = documents,
                FileName = fileName,
                Name = title
            };

      
            AfterCreate(d);

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

        private DocumentModel OpenClassDiagram(string fileName,
            UMLClassDiagram diagram, int lineNumber, string searchText)
        {
            var d = new ClassDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.ClassDocuments, old, @new), Configuration,
                _ioService)
            {
                DocumentType = DocumentTypes.Class,
                Content = File.ReadAllText(fileName),
                Diagram = diagram,
                FileName = fileName,
                Name = diagram.Title
            };
            lock (_docLock)
                OpenDocuments.Add(d);

            d.GotoLineNumber(lineNumber, searchText);

            this.CurrentDocument = d;

            return d;
        }

        private void OpenComponentDiagram(string fileName, UMLComponentDiagram diagram,
            int lineNumber, string searchText)
        {
            var d = new ComponentDiagramDocumentModel((old, @new) => { DiagramModelChanged(Documents.ComponentDiagrams, old, @new); }, Configuration, _ioService)
            {
                DocumentType = DocumentTypes.Class,
                Content = File.ReadAllText(fileName),
                Diagram = diagram,
                FileName = fileName,
                Name = diagram.Title
            };
            lock (_docLock)
                OpenDocuments.Add(d);

            d.GotoLineNumber(lineNumber, searchText);

            this.CurrentDocument = d;
        }

        private async Task OpenDirectoryHandler(bool? useAppSettings = false, string folder = null)
        {
            _folderBase = null;

            await SaveAll();
            string dir = null;
            if (folder == null)
            {
                dir = GetWorkingFolder(useAppSettings.GetValueOrDefault());
            }
            else
            {
                dir = GetWorkingFolder(useAppSettings.GetValueOrDefault(), folder);
            }
            if (string.IsNullOrEmpty(dir))
                return;

            SelectedMRUFolder = dir;
            foreach (var d in OpenDocuments)
            {
                d.Close();
            }
            if(OpenDocuments.Count > 0)
                OpenDocuments.Clear();

            AppSettings.Default.WorkingDir = dir;
            AppSettings.Default.Save();

            CreateNewComponentDiagram.RaiseCanExecuteChanged();
            CreateNewClassDiagram.RaiseCanExecuteChanged();
            CreateNewSequenceDiagram.RaiseCanExecuteChanged();
            SaveAllCommand.RaiseCanExecuteChanged();
            ScanAllFiles.RaiseCanExecuteChanged();

            await ScanDirectory(dir);

            if (!MRUFolders.Contains(dir))
                MRUFolders.Add(dir);
            AppSettings.Default.MRU = JsonConvert.SerializeObject(MRUFolders);

            AppSettings.Default.Save();
        }

        private string _selectedMRUFolder;
        public string SelectedMRUFolder
        {
            get
            {
                return _selectedMRUFolder;
            }
            set
            {
                SetValue(ref _selectedMRUFolder , value);
                if (!string.IsNullOrEmpty(_selectedMRUFolder) && value != _folderBase)
                    OpenDirectoryHandler(false, _selectedMRUFolder);

                
            }
        }

        private void GridSettingsChanged()
        {
            AppSettings.Default.GridSettings = JsonConvert.SerializeObject(this.GridSettings);
            AppSettings.Default.Save();
        }

        private void OpenDocuments_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var files = JsonConvert.DeserializeObject<List<string>>(AppSettings.Default.Files);
            if (files == null)
                files = new List<string>();

            if (e.NewItems != null)
            {
                if (!files.Any(p => e.NewItems.Contains(p)))
                {
                    files.Add(((DocumentModel)e.NewItems[0]).FileName);
                }
            }
            if (e.OldItems != null)
            {
                files.RemoveAll(p => p == ((DocumentModel)e.OldItems[0]).FileName);
            }
            if(e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                files.Clear();
            }
            AppSettings.Default.Files = JsonConvert.SerializeObject(files);
            AppSettings.Default.Save();
        }

        private void OpenSequenceDiagram(string fileName, UMLSequenceDiagram diagram,
            int lineNumber, string searchText)
        {
            var d = new SequenceDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.SequenceDiagrams, old, @new), Configuration, _ioService)
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

            d.GotoLineNumber(lineNumber, searchText);

            this.CurrentDocument = d;
        }

        private void OpenUnknownDiagram(string fullPath, UMLUnknownDiagram diagram)
        {
            var d = new UnknownDocumentModel((old, @new) =>
            {
            }, Configuration, _ioService)
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

        private void ProcessDataTypes()
        {
            DataTypes.Clear();

            var r = (from o in Documents.ClassDocuments
                     from z in o.DataTypes

                     orderby z.Namespace descending, z.Name descending
                     select Tuple.Create(o.FileName, z));

            foreach (var item in r)
                DataTypes.Add(item);
        }

        private async Task Save(DocumentModel doc)
        {
            await doc.Save();
        }

        private async Task SaveAll()
        {
            _fileSave.WaitOne();
            try
            {
                if (string.IsNullOrEmpty(_metaDataFile))
                {
                    return;
                }

                List<DocumentModel> c = new List<DocumentModel>();

                lock (_docLock)
                {
                    c = OpenDocuments.Where(p => p is ClassDiagramDocumentModel ||
                    p is SequenceDiagramDocumentModel ||
                    p is ComponentDiagramDocumentModel).ToList();
                }

                foreach (var file in c)
                {
                    await Save(file);
                }

                List<(UMLDiagram, UMLDiagram)> list = new List<(UMLDiagram, UMLDiagram)>();

                await UpdateDiagrams<ClassDiagramDocumentModel, UMLClassDiagram>(Documents.ClassDocuments);
                await UpdateDiagrams<SequenceDiagramDocumentModel, UMLSequenceDiagram>(Documents.SequenceDiagrams);
                await UpdateDiagrams<ComponentDiagramDocumentModel, UMLComponentDiagram>(Documents.ComponentDiagrams);
                ProcessDataTypes();
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

        private async Task ScanAllFilesHandler()
        {
            Documents.ClassDocuments.Clear();
            Documents.SequenceDiagrams.Clear();
            Documents.ComponentDiagrams.Clear();

            var folder = GetWorkingFolder();
            List<string> potentialSequenceDiagrams = new List<string>();
            await ScanForFiles(folder, potentialSequenceDiagrams);

            UMLDiagramTypeDiscovery discovery = new UMLDiagramTypeDiscovery();
            foreach (var seq in potentialSequenceDiagrams)
                await discovery.TryCreateSequenceDiagram(Documents, seq);

            ProcessDataTypes();

            foreach (var doc in OpenDocuments.OfType<SequenceDiagramDocumentModel>())
            {
                doc.UpdateDiagram(Documents.ClassDocuments);
            }
        }

        private async Task ScanDirectory(string dir)
        {
            Folder = new TreeViewModel(null, "", false, "", this);

            Folder.Children.Clear();

            var start = new TreeViewModel(Folder, dir, false, "", this);

            Folder.Children.Add(start);

            AddFolderItems(dir, start);

            await ScanAllFilesHandler();
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

        private async Task UpdateDiagrams<T1, T2>(List<T2> classDocuments) where T1 : DocumentModel where T2 : UMLDiagram
        {
            foreach (var document in OpenDocuments.OfType<T1>())
            {
                var e = await document.GetEditedDiagram();
                e.FileName = document.FileName;

                if (e is T2 cd)
                {
                    foreach (var oldCd in classDocuments)
                    {
                        if (oldCd.FileName == cd.FileName)
                        {
                            classDocuments.Remove(oldCd);
                            classDocuments.Add(cd);
                            break;
                        }
                    }
                }
            }
        }

        public void Change(string fullPath)
        {
            string dir = GetWorkingFolder();
            if (string.IsNullOrEmpty(dir))
                return;

            ScanDirectory(dir);
        }

        public async void GotoDataType(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var lb = (Tuple<string, UMLDataType>)e.AddedItems[0];
                await AttemptOpeningFile(lb.Item1, lb.Item2.LineNumber);
            }
        }

        public async void LoadedUI()
        {
            await OpenDirectoryHandler(true);

            var files = JsonConvert.DeserializeObject<List<string>>(AppSettings.Default.Files);
            if (files == null)
                files = new List<string>();

            foreach (var file in files)
            {
                if (File.Exists(file))
                    await AttemptOpeningFile(file);
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