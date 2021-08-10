﻿using Newtonsoft.Json;
using PlantUML;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
        public record DateTypeRecord(string FileName, UMLDataType DataType);

        private readonly ObservableCollection<DateTypeRecord> _dataTypes = new();
        private readonly IUMLDocumentCollectionSerialization _documentCollectionSerialization;
        private readonly IIOService _ioService;
        private readonly Timer _messageChecker;

        internal bool CloseAll()
        {
            DocumentModel[] dm = GetDocumentModelReadingArray();
            foreach (var item in dm)
            {
                if (item.IsDirty)
                    return true;

                item.TryClosePreview();
            }

            return false;
        }

        private bool _AllowContinue;
        private bool _confirmOpen;
        private readonly object _docLock = new();

        private string? _folderBase;
        private readonly DelegateCommand<string> _gotoDefinitionCommand;
        private string _metaDataDirectory = "";
        private string _metaDataFile = "";
        private GlobalFindResult? _selectedFindResult;
        private DocumentModel? currentDocument;
        private UMLModels.UMLDocumentCollection documents;
        private TreeViewModel folder;
        private DocumentMessage? selectedMessage;
        private readonly Lazy<GridSettings> _gridSettingLoader;

        public MainModel(IIOService openDirectoryService, IUMLDocumentCollectionSerialization documentCollectionSerialization)
        {
            _gotoDefinitionCommand = new DelegateCommand<string>(GotoDefinitionInvoked);
            documents = new UMLModels.UMLDocumentCollection();
            _messageChecker = new Timer(CheckMessages, null, Timeout.Infinite, Timeout.Infinite);
            _ioService = openDirectoryService;
            OpenDirectoryCommand = new DelegateCommand(() => OpenDirectoryHandler());

            SaveAllCommand = new DelegateCommand(SaveAllHandler, () => !string.IsNullOrEmpty(_folderBase));
            folder = new TreeViewModel(null, Path.GetTempPath(), false, "", this);
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
            ScanAllFiles = new DelegateCommand(async () => await ScanAllFilesHandler(), () => !string.IsNullOrEmpty(_folderBase));
            OpenTerminalCommand = new DelegateCommand(OpenTerminalHandler);
            OpenExplorerCommand = new DelegateCommand(OpenExplorerHandler);

            Configuration = new AppConfiguration("plantuml.jar");

            OpenDocuments.CollectionChanged += OpenDocuments_CollectionChanged;

            _gridSettingLoader = new Lazy<GridSettings>(() =>
            {
                var l = !string.IsNullOrEmpty(AppSettings.Default.GridSettings) ?
                JsonConvert.DeserializeObject<GridSettings>(AppSettings.Default.GridSettings) :
                new GridSettings();

                l.ChangedCB = GridSettingsChanged;

                return l;
            });




            WindowWidth = AppSettings.Default.WindowWidth;
            WindowHeight = AppSettings.Default.WindowHeight;
            WindowTop = AppSettings.Default.WindowTop;
            WindowLeft = AppSettings.Default.WindowLeft;

            _ = new Timer(MRULoader, null, 10, Timeout.Infinite);


        }

        private void MRULoader(object? state)
        {

            var mrus = JsonConvert.DeserializeObject<string[]>(AppSettings.Default.MRU ?? "[]");
            if (mrus != null)
                foreach (var s in mrus)
                {
                    MRUFolders.Add(s);
                }
        }


        private void OpenExplorerHandler()
        {
            if (string.IsNullOrEmpty(_folderBase))
                return;

            ProcessStartInfo psi = new()
            {
                UseShellExecute = true,
                WorkingDirectory = _folderBase,
                FileName = _folderBase
            };
            Process.Start(psi);

        }

        private void OpenTerminalHandler()
        {
            if (string.IsNullOrEmpty(_folderBase))
                return;
            try
            {
                ProcessStartInfo psi = new()
                {
                    UseShellExecute = true,
                    FileName = "wt",
                    WorkingDirectory = _folderBase
                };
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(_folderBase);

                Process.Start(psi);
            }
            catch (Win32Exception)
            {
                ProcessStartInfo psi = new()
                {
                    UseShellExecute = true,
                    FileName = "cmd",
                    WorkingDirectory = _folderBase
                };


                Process.Start(psi);

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

        public DocumentModel? CurrentDocument
        {
            get { return currentDocument; }
            set
            {
                if (currentDocument != null)
                {
                    currentDocument.Visible = Visibility.Collapsed;
                }

                SetValue(ref currentDocument, value);
                if (value != null)
                    value.Visible = Visibility.Visible;

            }
        }

        public ObservableCollection<DateTypeRecord> DataTypes
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
            get => _gridSettingLoader.Value;

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
        public DelegateCommand OpenTerminalCommand { get; }
        public DelegateCommand OpenExplorerCommand { get; }
        public DelegateCommand<DocumentModel> SelectDocumentCommand { get; }

        public GlobalFindResult? SelectedGlobalFindResult
        {
            get
            {
                return _selectedFindResult;
            }
            set
            {
                _selectedFindResult = value;
                if (value != null)
                    AttemptOpeningFile(value.FileName,
                      value.LineNumber, value.SearchText).ConfigureAwait(false);
            }
        }

        public DocumentMessage? SelectedMessage
        {
            get { return selectedMessage; }
            set
            {
                SetValue(ref selectedMessage, value);

                if (value != null && !string.IsNullOrEmpty(value.FileName))
                    AttemptOpeningFile(value.FileName, value.LineNumber).ConfigureAwait(false);
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
                if (Path.GetFileName(item).StartsWith(".", StringComparison.InvariantCulture))
                    continue;

                var fm = new TreeViewModel(model, item, false, "", this)
                {
                };
                model.Children.Add(fm);

                AddFolderItems(item, fm);
            }
        }

        private async Task AfterCreate(DocumentModel d)
        {
            lock (_docLock)
                OpenDocuments.Add(d);

            await d.Save();

            CurrentDocument = d;
        }

        private async Task AttemptOpeningFile(string fullPath,
            int lineNumber = 0, string? searchText = null)
        {


            DocumentModel? doc;
            lock (_docLock)
                doc = OpenDocuments.FirstOrDefault(p => p.FileName == fullPath);

            if (doc != null)
            {
                CurrentDocument = doc;
                doc.GotoLineNumber(lineNumber, searchText);
                return;
            }



            var (cd, sd, comd, ud) = await UMLDiagramTypeDiscovery.TryFindOrAddDocument(Documents, fullPath);


            if (cd != null)
            {
                await OpenClassDiagram(fullPath, cd, lineNumber, searchText);
            }
            else if (sd != null)
            {
                await OpenSequenceDiagram(fullPath, sd, lineNumber, searchText);
            }
            else if (comd != null)
                await OpenComponentDiagram(fullPath, comd, lineNumber, searchText);
            else if (ud != null)
            {
                await OpenUnknownDiagram(fullPath, ud);
            }

        }

        private async void CheckMessages(object? state)
        {
            if (string.IsNullOrEmpty(_metaDataFile) || string.IsNullOrEmpty(_folderBase))
            {
                _messageChecker.Change(2000, Timeout.Infinite);
                return;
            }

            if (Application.Current == null)
                return;
            List<UMLDiagram> diagrams = new();
            try
            {

                await UpdateDiagramDependencies();

            }
            catch
            {

            }

            try
            {
                DocumentModel[] dm = GetDocumentModelReadingArray();

                foreach (var doc in dm)
                {
                    var d = await doc.GetEditedDiagram();
                    if (d != null)
                    {
                        d.FileName = doc.FileName;
                        diagrams.Add(d);
                    }
                }

                foreach (var doc in Documents.ClassDocuments)
                {
                    if (!dm.Any(p => p.FileName == doc.FileName))
                        diagrams.Add(doc);
                }
                foreach (var doc in Documents.ComponentDiagrams)
                {
                    if (!dm.Any(p => p.FileName == doc.FileName))
                        diagrams.Add(doc);
                }
                foreach (var doc in Documents.SequenceDiagrams)
                {
                    if (!dm.Any(p => p.FileName == doc.FileName))
                        diagrams.Add(doc);
                }
            }
            catch
            {
                _messageChecker.Change(2000, Timeout.Infinite);
                return;
            }

            DocumentMessageGenerator documentMessageGenerator = new(diagrams);
            var newMessages = documentMessageGenerator.Generate(_folderBase);

            Application.Current.Dispatcher.Invoke(() =>
          {


              List<DocumentMessage> removals = new();
              foreach (var item in Messages)
              {

                  if (!newMessages.Any(z => string.CompareOrdinal(z.FileName, item.FileName) == 0 &&
                  string.CompareOrdinal(z.Text, item.Text) == 0 && z.LineNumber == item.LineNumber))
                  {
                      removals.Add(item);
                  }
              }

              removals.ForEach(p => Messages.Remove(p));

              foreach (var item in newMessages)
              {

                  if (!Messages.Any(z => string.CompareOrdinal(z.FileName, item.FileName) == 0 &&
                 string.CompareOrdinal(z.Text, item.Text) == 0 && z.LineNumber == item.LineNumber))
                  {
                      Messages.Add(item);
                  }
              }


              foreach (var d in Messages)
              {
                  if (d is MissingMethodDocumentMessage || d is MissingDataTypeMessage)
                      d.FixingCommand = new DelegateCommand<DocumentMessage>(FixingCommandHandler);

                  lock (_docLock)
                  {
                      var docs = OpenDocuments.Where(p => string.CompareOrdinal(p.FileName, d.FileName) == 0);
                      foreach (var doc in docs)
                      {
                          if (CurrentDocument == doc)
                              doc.ReportMessage(d);
                      }
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

            await Save(doc);

            await UpdateDiagramDependencies();


            Close(doc);

            await ScanAllFilesHandler();
        }

        private void CloseDocumentHandler(DocumentModel doc)
        {
            if (doc.IsDirty)
            {
            }
            Close(doc);
        }



        private async void FixingCommandHandler(DocumentMessage sender)
        {
            DocumentModel[] dm = GetDocumentModelReadingArray();



            if (sender is MissingMethodDocumentMessage missingMethodMessage)
            {
                foreach (var doc in Documents.ClassDocuments)
                {
                    var d = doc.DataTypes.FirstOrDefault(p => p.Id == missingMethodMessage.MissingMethodDataTypeId);
                    if (d == null)
                        continue;
                    if (missingMethodMessage.MissingMethodText == null)
                        continue;

                    UMLClassDiagramParser.TryParseLineForDataType(missingMethodMessage.MissingMethodText.Trim(),
                        new Dictionary<string, UMLDataType>(), d);

                    var od = dm.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => string.CompareOrdinal(p.FileName, doc.FileName) == 0);
                    if (od != null)
                    {
                        CurrentDocument = od;
                        od.UpdateDiagram(doc);
                    }
                    else
                    {
                        await OpenClassDiagram(doc.FileName, doc, 0, null);
                    }
                }
            }
            else if (sender is MissingDataTypeMessage missingDataTypeMessage)
            {
                var f = Documents.ClassDocuments.FirstOrDefault(p => string.CompareOrdinal( p.Title , "defaults.class") == 0);
                if (f != null)
                {
                    if (f.Package == null)
                        f.Package = new UMLPackage("defaults");

                    f.Package.Children.Add(new UMLClass("default", false, missingDataTypeMessage.MissingDataTypeName, new List<UMLDataType>()));

                    var wf = GetWorkingFolder(true);
                    if (wf == null)
                        return;

                    string d = Path.Combine(wf, "defaults.class.puml");

                    var od = dm.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => p.FileName == d);
                    if (od != null)
                    {
                        CurrentDocument = od;
                        od.UpdateDiagram(f);
                    }
                    else
                    {
                        await OpenClassDiagram(d, f, 0, null);

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

        private string? GetNewFile(string fileExtension)
        {
            TreeViewModel? selected = GetSelectedFolder(Folder);
            if (selected == null)
                return null;

            string? dir = selected?.FullPath ?? _folderBase;

            if (string.IsNullOrEmpty(dir))
                return null;

            string? nf = _ioService.NewFile(dir, fileExtension);
            return nf;
        }

        private TreeViewModel? GetSelectedFile(TreeViewModel item)
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

        private TreeViewModel? GetSelectedFolder(TreeViewModel item)
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

        private string? GetWorkingFolder(bool useAppSettingIfFound = false, string? folder = null)
        {
            if (useAppSettingIfFound)
            {
                _folderBase = AppSettings.Default.WorkingDir;
            }

            if (string.IsNullOrEmpty(_folderBase))
            {
                string? dir = folder;
                if (folder == null)
                {
                    dir = _ioService.GetDirectory();
                    if (string.IsNullOrEmpty(dir))
                        return null;
                }

                _folderBase = dir;
            }

            if (_folderBase == null)
                return null;

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
            if (string.IsNullOrEmpty(_folderBase))
                return;

            var findresults = await GlobalSearch.Find(_folderBase, obj, new string[]
            {"*.puml"
            });
            GlobalFindResults.Clear();
            foreach (var f in findresults)
                GlobalFindResults.Add(f);
        }

        private async void GotoDefinitionInvoked(string text)
        {
            foreach (var item in Documents.ClassDocuments.Where(z =>
            z.DataTypes.Any(v => string.CompareOrdinal(v.Name, text) == 0)).Select(p => new
            {
                FN = p.FileName,
                D = p,
                DT = p.DataTypes.First(z => z.Name == text)
            }))
            {
                await AttemptOpeningFile(item.FN, item.DT.LineNumber, null);
               
            }
        }

        private async Task NewClassDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLClassDiagram(title, fileName);
            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";
            var d = new ClassDiagramDocumentModel(
                Configuration, _ioService, model, Documents.ClassDocuments, fileName, title, content);

            Documents.ClassDocuments.Add(model);

            await AfterCreate(d);
        }

        private async void NewClassDiagramHandler()
        {
            string? nf = GetNewFile(".class.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewClassDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }

            await ScanDirectory(_folderBase);
        }

        private async Task NewComponentDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLComponentDiagram(title, fileName);

            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";
            var d = new ComponentDiagramDocumentModel(Configuration, _ioService,  model, fileName, title, content);
            

            Documents.ComponentDiagrams.Add(model);
            await AfterCreate(d);
        }

        private async void NewComponentDiagramHandler()
        {
            string? nf = GetNewFile(".component.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewComponentDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }

            await ScanDirectory(_folderBase);
        }

        private async Task NewSequenceDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLSequenceDiagram(title, fileName);
            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";

            var d = new SequenceDiagramDocumentModel(Configuration, _ioService, model, Documents.ClassDocuments, fileName, title, content);

            Documents.SequenceDiagrams.Add(model);
            await AfterCreate(d);
        }
        private async void NewUnknownDiagramHandler()
        {
            string? nf = GetNewFile(".puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewUnknownUMLDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }

            await ScanDirectory(_folderBase);
        }

        private async Task NewUnknownUMLDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLUnknownDiagram(title, fileName);
            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";
            var d = new UnknownDocumentModel((old, @new) =>
            { },
            Configuration, _ioService, model, Documents, fileName, title, content);


            await AfterCreate(d);

        }

        private async void NewSequenceDiagramHandler()
        {
            string? nf = GetNewFile(".seq.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewSequenceDiagram(nf, Path.GetFileNameWithoutExtension(nf));
            }

            await ScanDirectory(_folderBase);
        }

        private async Task<DocumentModel> OpenClassDiagram(string fileName,
            UMLClassDiagram diagram, int lineNumber, string? searchText = null)
        {
            var content = await  File.ReadAllTextAsync(fileName);
            var d = new ClassDiagramDocumentModel(Configuration,
                _ioService,   diagram, Documents.ClassDocuments, fileName, diagram.Title, content);
         
            lock (_docLock)
                OpenDocuments.Add(d);

            d.GotoLineNumber(lineNumber, searchText);

            CurrentDocument = d;

            return d;
        }

        private async Task OpenComponentDiagram(string fileName, UMLComponentDiagram diagram,
            int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fileName);
            var d = new ComponentDiagramDocumentModel(Configuration, _ioService, diagram, fileName, diagram.Title, content);
            
            lock (_docLock)
                OpenDocuments.Add(d);

            d.GotoLineNumber(lineNumber, searchText);

            CurrentDocument = d;
        }

        private async void OpenDirectoryHandler(bool? useAppSettings = false, string? folder = null)
        {


            await SaveAll();
            _folderBase = null;
            string? dir;
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
            lock (_docLock)
            {
                foreach (var d in OpenDocuments)
                {
                    d.Close();
                }

                if (OpenDocuments.Count > 0)
                    OpenDocuments.Clear();
            }
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

        private string? _selectedMRUFolder;
        public string? SelectedMRUFolder
        {
            get
            {
                return _selectedMRUFolder;
            }
            set
            {
                SetValue(ref _selectedMRUFolder, value);
                if (!string.IsNullOrEmpty(_selectedMRUFolder) && value != _folderBase)
                    OpenDirectoryHandler(false, _selectedMRUFolder);


            }
        }

        private void GridSettingsChanged()
        {
            AppSettings.Default.GridSettings = JsonConvert.SerializeObject(GridSettings);
            AppSettings.Default.Save();
        }

        private void OpenDocuments_CollectionChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var files = JsonConvert.DeserializeObject<List<string>>(AppSettings.Default.Files);
            if (files == null)
                files = new List<string>();

            if (e.NewItems != null)
            {
                if (!files.Any(p => e.NewItems.Contains(p)))
                {
                    var dm = (DocumentModel?)e.NewItems[0];
                    if (dm != null)
                        files.Add(dm.FileName);
                }
            }
            if (e.OldItems != null)
            {
                var dm = (DocumentModel?)e.OldItems[0];
                if (dm != null)
                    files.RemoveAll(p => string.CompareOrdinal(p, dm.FileName) == 0);
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                files.Clear();
            }
            AppSettings.Default.Files = JsonConvert.SerializeObject(files);
            AppSettings.Default.Save();
        }

        private async Task OpenSequenceDiagram(string fileName, UMLSequenceDiagram diagram,
            int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fileName);

            var d = new SequenceDiagramDocumentModel(Configuration, _ioService, diagram, Documents.ClassDocuments, fileName, diagram.Title, content);
           
            lock (_docLock)
                OpenDocuments.Add(d);

            d.GotoLineNumber(lineNumber, searchText);

            CurrentDocument = d;
        }

        private async Task OpenUnknownDiagram(string fullPath, UMLUnknownDiagram diagram)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            var d = new UnknownDocumentModel((old, @new) =>
            {
            }, Configuration, _ioService, diagram, Documents, fullPath, diagram.Title, content);

            lock (_docLock)
                OpenDocuments.Add(d);
            
            CurrentDocument = d;
        }

        private void ProcessDataTypes()
        {

            var dt = DataTypes.ToList();

            var dataTypes = (from o in Documents.ClassDocuments
                             from z in o.DataTypes
                             select new DateTypeRecord(o.FileName, z)).ToArray();

            bool isDirty = false;
            foreach (var r in dt.ToArray())
            {
                if (!dataTypes.Contains(r))
                {
                    dt.Remove(r);
                    isDirty = true;
                }
            }

            foreach (var r in dataTypes)
                if (!dt.Contains(r))
                {
                    dt.Add(r);
                    isDirty = true;
                }
            if (isDirty)
            {
                DataTypes.Clear();
                foreach (var d in dt.OrderBy(z => z.DataType.Namespace).ThenBy(z => z.DataType.Name))
                {
                    DataTypes.Add(d);
                }
            }




        }

        private static async Task Save(DocumentModel doc)
        {
            await doc.Save();


        }

        private async Task SaveAll()
        {


            if (string.IsNullOrEmpty(_metaDataFile))
            {
                return;
            }

            List<DocumentModel> c = new();

            lock (_docLock)
            {
                c = OpenDocuments.Where(p => p.IsDirty).ToList();
            }

            foreach (var file in c)
            {
                await Save(file);
            }

            await UpdateDiagramDependencies();


            await ScanAllFilesHandler();

        }

        private async Task UpdateDiagramDependencies()
        {
            DocumentModel[] dm = GetDocumentModelReadingArray();

            List<(UMLDiagram, UMLDiagram)> list = new();

            await UpdateDiagrams<ClassDiagramDocumentModel, UMLClassDiagram>(dm, Documents.ClassDocuments);
            await UpdateDiagrams<SequenceDiagramDocumentModel, UMLSequenceDiagram>(dm, Documents.SequenceDiagrams);
            await UpdateDiagrams<ComponentDiagramDocumentModel, UMLComponentDiagram>(dm, Documents.ComponentDiagrams);


            var docs = Application.Current.Dispatcher.Invoke(() =>
            {
                ProcessDataTypes();
                lock (_docLock)
                    return OpenDocuments.OfType<SequenceDiagramDocumentModel>().ToArray();
            });


            foreach (var document in docs)
            {
                document.UpdateDiagram(Documents.ClassDocuments);
            }

            await _documentCollectionSerialization.Save(Documents, _metaDataFile);
        }

        private DocumentModel[] GetDocumentModelReadingArray()
        {
            DocumentModel[] dm;
            lock (_docLock)
                dm = OpenDocuments.ToArray();
            return dm;
        }

        private async void SaveAllHandler()
        {
            await SaveAll();

        }

        private async void SaveCommandHandler(DocumentModel doc)
        {

            await Save(doc);
            await UpdateDiagramDependencies();


            await ScanAllFilesHandler();

        }

        private async Task ScanAllFilesHandler()
        {
            Documents.ClassDocuments.Clear();
            Documents.SequenceDiagrams.Clear();
            Documents.ComponentDiagrams.Clear();

            var folder = GetWorkingFolder();
            if (folder == null)
                return;

            List<string> potentialSequenceDiagrams = new();
            await ScanForFiles(folder, potentialSequenceDiagrams);


            foreach (var seq in potentialSequenceDiagrams)
                await UMLDiagramTypeDiscovery.TryCreateSequenceDiagram(Documents, seq);

            ProcessDataTypes();

            foreach (var doc in OpenDocuments.OfType<SequenceDiagramDocumentModel>())
            {
                doc.UpdateDiagram(Documents.ClassDocuments);
            }
        }

        private async Task ScanDirectory(string? dir)
        {
            if (dir == null)
                return;

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

                if (null == await UMLDiagramTypeDiscovery.TryCreateClassDiagram(Documents, file))
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

        private static async Task UpdateDiagrams<T1, T2>(DocumentModel[] documentModels, List<T2> classDocuments) where T1 : DocumentModel where T2 : UMLDiagram
        {
            foreach (var document in documentModels.OfType<T1>())
            {
                var e = await document.GetEditedDiagram();
                if (e == null)
                    continue;

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

        public async Task Change(string fullPath)
        {
            string? dir = GetWorkingFolder();
            if (string.IsNullOrEmpty(dir))
                return;

            await ScanDirectory(dir);
        }

        public async void GotoDataType(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var lb = (DateTypeRecord?)e.AddedItems[0];
                if (lb != null)
                    await AttemptOpeningFile(lb.FileName, lb.DataType.LineNumber);
            }
        }

        public async void LoadedUI()
        {
            OpenDirectoryHandler(true);

            var files = JsonConvert.DeserializeObject<List<string>>(AppSettings.Default.Files);
            if (files == null)
                files = new List<string>();

            foreach (var file in files)
            {
                if (File.Exists(file))
                    await AttemptOpeningFile(file);
            }

            _messageChecker.Change(1000, Timeout.Infinite);
        }

        public async void TreeItemClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                return;

            TreeViewModel? fbm = GetSelectedFile(Folder);
            if (fbm == null)
                return;

            await AttemptOpeningFile(fbm.FullPath);
        }
    }
}