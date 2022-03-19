using Newtonsoft.Json;
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
    internal class MainModel : BindingBase
    {
        public record DateTypeRecord(string FileName, UMLDataType DataType);

        private readonly SemaphoreSlim _checkMessagesRunning = new(1, 1);
        private readonly ObservableCollection<DateTypeRecord> _dataTypes = new();
        private readonly object _docLock = new();
        private readonly IUMLDocumentCollectionSerialization _documentCollectionSerialization;
        private readonly DelegateCommand<string> _gotoDefinitionCommand;
        private readonly Lazy<GridSettings> _gridSettingLoader;
        private readonly IIOService _ioService;
        private readonly Timer _messageChecker;

        private bool _AllowContinue;

        private CancellationTokenSource? _cancelCurrentExecutingAction;

        private bool _confirmOpen;

        private string? _currentActionExecuting;

        private string? _folderBase;

        private string _metaDataDirectory = "";

        private string _metaDataFile = "";

        private GlobalFindResult? _selectedFindResult;

        private string? _selectedMRUFolder;

        private BaseDocumentModel? currentDocument;

        private UMLModels.UMLDocumentCollection documents;

        private TreeViewModel folder;

        private DocumentMessage? selectedMessage;

        private readonly MainWindow _window;
        private string? _gitMessages;

        public string? GitMessages

        {
            get => _gitMessages;
            set => SetValue(ref _gitMessages, value);
        }
        public MainModel(IIOService openDirectoryService, IUMLDocumentCollectionSerialization documentCollectionSerialization, MainWindow mainWindow)
        {
            _window = mainWindow;
            CancelExecutingAction = new DelegateCommand(CancelCurrentExecutingAction, () =>
            {
                return _cancelCurrentExecutingAction != null;
            });

            _gotoDefinitionCommand = new DelegateCommand<string>(GotoDefinitionInvoked);
            documents = new UMLModels.UMLDocumentCollection();
            _messageChecker = new Timer(CheckMessages, null, Timeout.Infinite, Timeout.Infinite);
            _ioService = openDirectoryService;
            OpenDirectoryCommand = new DelegateCommand(() => _ = OpenDirectoryHandler());

            SaveAllCommand = new DelegateCommand(SaveAllHandler, () => !string.IsNullOrEmpty(_folderBase));
            folder = new FolderTreeViewModel(null, Path.GetTempPath(), true);
            _documentCollectionSerialization = documentCollectionSerialization;
            OpenDocuments = new ObservableCollection<BaseDocumentModel>();
            CreateNewUnknownDiagram = new DelegateCommand(NewUnknownDiagramHandler, () => !string.IsNullOrEmpty(_folderBase));

            CreateNewSequenceDiagram = new DelegateCommand(NewSequenceDiagramHandler, () => !string.IsNullOrEmpty(_folderBase));
            CreateNewClassDiagram = new DelegateCommand(NewClassDiagramHandler, () => !string.IsNullOrEmpty(_folderBase));
            CreateNewComponentDiagram = new DelegateCommand(NewComponentDiagramHandler, () => !string.IsNullOrEmpty(_folderBase));
            CreateMarkDownDocument = new DelegateCommand(NewMarkDownDocumentHandler, () => !string.IsNullOrEmpty(_folderBase));
            CreateYAMLDocument = new DelegateCommand(NewYAMLDocumentHandler, () => !string.IsNullOrEmpty(_folderBase));
            CreateUMLImage = new DelegateCommand(CreateUMLImageHandler, () => _selectedFile is not null);
            GitCommitAndSyncCommand = new DelegateCommand(GitCommitAndSyncCommandHandler, () => !string.IsNullOrEmpty(_folderBase));


            CloseDocument = new DelegateCommand<BaseDocumentModel>(CloseDocumentHandler);
            CloseDocumentAndSave = new DelegateCommand<BaseDocumentModel>(CloseDocumentAndSaveHandler);
            SaveCommand = new DelegateCommand<BaseDocumentModel>(SaveCommandHandler);
            Messages = new ObservableCollection<DocumentMessage>();
            SelectDocumentCommand = new DelegateCommand<BaseDocumentModel>(SelectDocumentHandler);
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

        private async void GitCommitAndSyncCommandHandler()
        {
            GitMessages = null;
            GitSupport gs = new GitSupport();
            if (string.IsNullOrEmpty(_folderBase))
            {
                return;
            }

            GitMessages = await gs.CommitAndSync(_folderBase);

        }

        private async void CreateUMLImageHandler()
        {
            if (_selectedFile is null)
            {
                return;
            }

            string? dir = Path.GetDirectoryName(_selectedFile.FullPath);
            if (dir is null)
            {
                return;
            }

            PlantUMLImageGenerator generator = new PlantUMLImageGenerator(Configuration.JarLocation,
                _selectedFile.FullPath, dir);


            var folder = FindFolderContaining(Folder, _selectedFile.FullPath);


            var res = await generator.Create();

            if (folder != null)
            {
                var file = folder.Children.First(z => string.Equals(z.FullPath, _selectedFile.FullPath, StringComparison.Ordinal));

                var ix = folder.Children.IndexOf(file);
                folder.Children.Insert(ix, new TreeViewModel(folder, res.fileName, GetIcon(res.fileName)));
            }


        }

        private TreeViewModel? FindFolderContaining(TreeViewModel root, string selectedFile)
        {

            foreach (var f in root.Children)
            {
                if (f.IsFile)
                {
                    if (string.Equals(f.FullPath, selectedFile, StringComparison.Ordinal))
                    {
                        return root;
                    }
                }
                else
                {
                    var g = FindFolderContaining(f, selectedFile);
                    if (g != null)
                    {
                        return g;
                    }
                }
            }

            return null;
        }

        private AppConfiguration Configuration
        {
            get;
        }

        public bool AllowContinue
        {
            get => _AllowContinue;
            set => SetValue(ref _AllowContinue, value);
        }

        public ICommand CancelExecutingAction
        {
            get;
        }

        public DelegateCommand<BaseDocumentModel> CloseDocument
        {
            get;
        }

        public DelegateCommand<BaseDocumentModel> CloseDocumentAndSave
        {
            get;
        }

        private TaskCompletionSource? _continueClosinTaskSource;
        private TreeViewModel? _selectedFile;
        private TreeViewModel? _selectedFolder;

        public bool ConfirmOpen
        {
            get => _confirmOpen;
            set
            {

                if (value && !_confirmOpen)
                {

                    _continueClosinTaskSource = new TaskCompletionSource();

                }
                else if (!value && _confirmOpen)
                {
                    _continueClosinTaskSource?.SetResult();
                }

                SetValue(ref _confirmOpen, value);
            }
        }

        public DelegateCommand CreateNewClassDiagram
        {
            get;
        }

        public DelegateCommand CreateNewComponentDiagram
        {
            get;
        }
        public DelegateCommand CreateYAMLDocument
        {
            get;
        }

        public DelegateCommand CreateMarkDownDocument
        {
            get;
        }
        public DelegateCommand CreateNewSequenceDiagram
        {
            get;
        }

        public DelegateCommand CreateNewUnknownDiagram
        {
            get;
        }

        public DelegateCommand CreateUMLImage
        {
            get;
        }
        public DelegateCommand GitCommitAndSyncCommand
        {
            get;
        }

        public string? CurrentActionExecuting
        {
            get => _currentActionExecuting;
            set
            {
                SetValue(ref _currentActionExecuting, value);
                if (value != null && _cancelCurrentExecutingAction == null)
                {
                    _cancelCurrentExecutingAction?.Dispose();
                    _cancelCurrentExecutingAction = new CancellationTokenSource();
                }
                else if (value == null)
                {
                    _cancelCurrentExecutingAction?.Dispose();
                    _cancelCurrentExecutingAction = null;
                }

                ((DelegateCommand)CancelExecutingAction).RaiseCanExecuteChanged();
            }
        }

        public BaseDocumentModel? CurrentDocument
        {
            get => currentDocument;
            set
            {
                if (currentDocument != null)
                {
                    currentDocument.Visible = Visibility.Collapsed;
                }

                SetValue(ref currentDocument, value);
                if (value != null)
                {
                    value.Visible = Visibility.Visible;
                }
            }
        }

        public ObservableCollection<DateTypeRecord> DataTypes => _dataTypes;

        public UMLModels.UMLDocumentCollection Documents
        {
            get => documents;
            set => SetValue(ref documents, value);
        }

        public TreeViewModel Folder
        {
            get => folder;
            private set => SetValue(ref folder, value);
        }

        public ObservableCollection<GlobalFindResult> GlobalFindResults { get; } = new ObservableCollection<GlobalFindResult>();

        public DelegateCommand<string> GlobalSearchCommand
        {
            get;
        }

        public DelegateCommand<string> GotoDefinitionCommand => _gotoDefinitionCommand;

        public GridSettings GridSettings => _gridSettingLoader.Value;

        public string JarLocation
        {
            get => Configuration.JarLocation;
            set => Configuration.JarLocation = value;
        }

        public ObservableCollection<DocumentMessage> Messages
        {
            get;
        }

        public ObservableCollection<string> MRUFolders { get; } = new ObservableCollection<string>();

        public ICommand OpenDirectoryCommand
        {
            get;
        }

        public ObservableCollection<BaseDocumentModel> OpenDocuments
        {
            get;
        }

        public DelegateCommand OpenExplorerCommand
        {
            get;
        }

        public DelegateCommand OpenTerminalCommand
        {
            get;
        }

        public DelegateCommand SaveAllCommand
        {
            get;
        }

        public DelegateCommand<BaseDocumentModel> SaveCommand
        {
            get;
        }

        public DelegateCommand ScanAllFiles
        {
            get;
        }

        public DelegateCommand<BaseDocumentModel> SelectDocumentCommand
        {
            get;
        }

        public GlobalFindResult? SelectedGlobalFindResult
        {
            get => _selectedFindResult;
            set
            {
                _selectedFindResult = value;
                if (value != null)
                {
                    AttemptOpeningFile(value.FileName,
                      value.LineNumber, value.SearchText).ConfigureAwait(false);
                }
            }
        }

        public DocumentMessage? SelectedMessage
        {
            get => selectedMessage;
            set
            {
                SetValue(ref selectedMessage, value);

                if (value != null && !string.IsNullOrEmpty(value.FileName))
                {
                    AttemptOpeningFile(value.FileName, value.LineNumber).ConfigureAwait(false);
                }
            }
        }

        public string? SelectedMRUFolder
        {
            get => _selectedMRUFolder;
            set
            {
                SetValue(ref _selectedMRUFolder, value);
                if (!string.IsNullOrEmpty(_selectedMRUFolder) && value != _folderBase)
                {
                    _ = OpenDirectoryHandler(false, _selectedMRUFolder);
                }
            }
        }

        public int WindowHeight
        {
            get; set;
        }

        public int WindowLeft
        {
            get; set;
        }

        public int WindowTop
        {
            get; set;
        }

        public int WindowWidth
        {
            get; set;
        }

        private static async Task Save(TextDocumentModel doc)
        {
            await doc.Save();
        }

        private static async Task UpdateDiagrams<T1, T2>(TextDocumentModel[] documentModels, LockedList<T2> classDocuments) where T1 : TextDocumentModel where T2 : UMLDiagram
        {
            foreach (var document in documentModels.OfType<T1>())
            {
                var e = await document.GetEditedDiagram();
                if (e == null)
                {
                    continue;
                }

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

        private async Task AddFolderItems(string dir, TreeViewModel model)
        {
            if ((_cancelCurrentExecutingAction?.IsCancellationRequested).GetValueOrDefault())
            {
                return;
            }

            CurrentActionExecuting = $"Reading {dir}";

            await Task.Delay(1); //sleep for ui updates



            foreach (var file in Directory.EnumerateFiles(dir))
            {
                switch (Path.GetExtension(file))
                {
                    case ".md":
                    case ".yml":
                    case ".png":
                    case ".jpg":
                    case ".puml":
                        model.Children.Add(new TreeViewModel(model, file, GetIcon(file)));
                        break;

                }

            }

            var fp = new FoldersStatusPersistance();
            var closed = fp.GetClosedFolders();

            foreach (var item in Directory.EnumerateDirectories(dir))
            {
                if (Path.GetFileName(item).StartsWith(".", StringComparison.InvariantCulture))
                {
                    continue;
                }

                bool isExpanded = true;
                if (closed.Contains(item))
                {
                    isExpanded = false;
                }

                var fm = new FolderTreeViewModel(model, item, isExpanded);
                model.Children.Add(fm);

                await AddFolderItems(item, fm);
            }
        }

        private string GetIcon(string file)
        {
            if (file.Contains(".component.puml"))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/com.png";
            }
            else if (file.Contains(".class.puml"))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/class.png";
            }
            else if (file.Contains(".seq.puml"))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/sequence.png";
            }
            else if (file.Contains(".md"))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/md.png";
            }
            else if (file.Contains(".yml"))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/yml.png";
            }
            else if (file.Contains(".png") || file.Contains(".jpg"))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/emblem_512.png";
            }
            else
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/uml.png";

            }

        }

        private async Task AfterCreate(TextDocumentModel d)
        {
            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }

            await d.Save();

            CurrentDocument = d;
        }

        private async Task AttemptOpeningFile(string fullPath,
            int lineNumber = 0, string? searchText = null)
        {
            BaseDocumentModel? doc;
            lock (_docLock)
            {
                doc = OpenDocuments.FirstOrDefault(p => p.FileName == fullPath);
            }

            if (doc != null)
            {
                CurrentDocument = doc;
                if (doc is TextDocumentModel textDocument)
                {
                    textDocument.GotoLineNumber(lineNumber, searchText);
                }

                return;
            }

            if (string.Compare(Path.GetExtension(fullPath), ".puml", StringComparison.OrdinalIgnoreCase) == 0)
            {


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
                {
                    await OpenComponentDiagram(fullPath, comd, lineNumber, searchText);
                }
                else if (ud != null)
                {
                    await OpenUnknownDiagram(fullPath, ud);
                }
            }
            else if (string.Compare(Path.GetExtension(fullPath), ".md", StringComparison.OrdinalIgnoreCase) == 0)
            {
                await OpenMarkDownFile(fullPath, lineNumber, searchText);
            }
            else if (string.Compare(Path.GetExtension(fullPath), ".yml", StringComparison.OrdinalIgnoreCase) == 0)
            {
                await OpenYMLFile(fullPath, lineNumber, searchText);
            }
            else if (string.Compare(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(Path.GetExtension(fullPath), ".jpg", StringComparison.OrdinalIgnoreCase) == 0)
            {
                await OpenImageFile(fullPath);
            }
        }
        private async Task OpenImageFile(string fullPath)
        {

            var d = new ImageDocumentModel(
                fullPath,
                Path.GetFileName(fullPath));
            await d.Init();
            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }

            CurrentDocument = d;
        }

        private async Task OpenYMLFile(string fullPath, int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            var d = new YMLDocumentModel(Configuration, _ioService,
                fullPath,
                Path.GetFileName(fullPath)
                , content);

            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }
            d.GotoLineNumber(lineNumber, searchText);
            CurrentDocument = d;
        }
        private async Task OpenMarkDownFile(string fullPath, int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            var d = new MDDocumentModel(Configuration, _ioService,
                fullPath,
                Path.GetFileName(fullPath)
                , content);

            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }
            d.GotoLineNumber(lineNumber, searchText);
            CurrentDocument = d;
        }

        private void CancelCurrentExecutingAction()
        {
            _cancelCurrentExecutingAction?.Cancel();
        }

        private async void CheckMessages(object? state)
        {
            await _checkMessagesRunning.WaitAsync();
            try
            {
                if (string.IsNullOrEmpty(_metaDataFile) || string.IsNullOrEmpty(_folderBase))
                {
                    _messageChecker.Change(2000, Timeout.Infinite);
                    return;
                }

                if (Application.Current == null)
                {
                    return;
                }

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
                    TextDocumentModel[] dm = GetTextDocumentModelReadingArray();

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
                        {
                            diagrams.Add(doc);
                        }
                    }
                    foreach (var doc in Documents.ComponentDiagrams)
                    {
                        if (!dm.Any(p => p.FileName == doc.FileName))
                        {
                            diagrams.Add(doc);
                        }
                    }
                    foreach (var doc in Documents.SequenceDiagrams)
                    {
                        if (!dm.Any(p => p.FileName == doc.FileName))
                        {
                            diagrams.Add(doc);
                        }
                    }
                }
                catch
                {
                    _messageChecker.Change(2000, Timeout.Infinite);
                    return;
                }

                DocumentMessageGenerator documentMessageGenerator = new(diagrams);
                var newMessages = documentMessageGenerator.Generate(_folderBase);

                Application.Current?.Dispatcher.Invoke(() =>
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
                        if ((d is MissingMethodDocumentMessage || d is MissingDataTypeMessage) && d.FixingCommand is null)
                        {
                            d.FixingCommand = new DelegateCommand<DocumentMessage>(FixingCommandHandler);
                        }

                        lock (_docLock)
                        {
                            var docs = OpenDocuments.Where(p => string.Equals(p.FileName, d.FileName, StringComparison.Ordinal));
                            foreach (var doc in docs)
                            {
                                if (CurrentDocument == doc && doc is TextDocumentModel textDoc)
                                {
                                    textDoc.ReportMessage(d);
                                }
                            }
                        }
                    }

                    _messageChecker.Change(2000, Timeout.Infinite);
                });
            }
            finally
            {
                _checkMessagesRunning.Release();
            }
        }

        private void Close(BaseDocumentModel doc)
        {
            doc.Close();
            lock (_docLock)
            {
                OpenDocuments.Remove(doc);
            }

            CurrentDocument = OpenDocuments.LastOrDefault();
        }

        private async void CloseDocumentAndSaveHandler(BaseDocumentModel doc)
        {
            if (doc is TextDocumentModel textDocument)
            {
                await Save(textDocument);
            }

            await UpdateDiagramDependencies();

            Close(doc);

            await ScanAllFilesHandler();
        }

        private void CloseDocumentHandler(BaseDocumentModel doc)
        {
            if (doc.IsDirty)
            {
            }
            Close(doc);
        }

        private async void FixingCommandHandler(DocumentMessage sender)
        {
            TextDocumentModel[] dm = GetTextDocumentModelReadingArray();

            if (sender is MissingMethodDocumentMessage missingMethodMessage)
            {
                foreach (var doc in Documents.ClassDocuments)
                {
                    var d = doc.DataTypes.FirstOrDefault(p => p.Id == missingMethodMessage.MissingMethodDataTypeId);
                    if (d == null)
                    {
                        continue;
                    }

                    if (missingMethodMessage.MissingMethodText == null)
                    {
                        continue;
                    }

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
                var f = Documents.ClassDocuments.FirstOrDefault(p => string.CompareOrdinal(p.Title, "defaults.class") == 0);
                if (f != null)
                {


                    f.Package.Children.Add(new UMLClass("default", false, missingDataTypeMessage.MissingDataTypeName, new List<UMLDataType>()));

                    var wf = GetWorkingFolder(true);
                    if (wf == null)
                    {
                        return;
                    }

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

        private TextDocumentModel[] GetTextDocumentModelReadingArray()
        {
            TextDocumentModel[] dm;
            lock (_docLock)
            {
                dm = OpenDocuments.OfType<TextDocumentModel>().ToArray();
            }

            return dm;
        }

        private string? GetNewFile(string fileExtension)
        {
            TreeViewModel? selected = _selectedFolder;
            if (selected == null)
            {
                return null;
            }

            string? dir = selected?.FullPath ?? _folderBase;

            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }

            string? nf = _ioService.NewFile(dir, fileExtension);
            return nf;
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
                    {
                        return null;
                    }
                }

                _folderBase = dir;
            }

            if (_folderBase == null)
            {
                return null;
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
            if (string.IsNullOrEmpty(_folderBase))
            {
                return;
            }

            var findresults = await GlobalSearch.Find(_folderBase, obj, new string[]
            {"*.puml", "*.md", "*.yml"
            });
            GlobalFindResults.Clear();
            foreach (var f in findresults)
            {
                GlobalFindResults.Add(f);
            }
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

        private void GridSettingsChanged()
        {
            AppSettings.Default.GridSettings = JsonConvert.SerializeObject(GridSettings);
            AppSettings.Default.Save();
        }

        private void MRULoader(object? state)
        {
            var mrus = JsonConvert.DeserializeObject<string[]>(AppSettings.Default.MRU ?? "[]");
            if (mrus != null)
            {
                foreach (var s in mrus)
                {
                    MRUFolders.Add(s);
                }
            }
        }

        private async Task NewClassDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLClassDiagram(title, fileName, null);
            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";
            var d = new ClassDiagramDocumentModel(
                Configuration, _ioService, model, Documents.ClassDocuments, fileName, title, content);

            Documents.ClassDocuments.Add(model);

            await AfterCreate(d);
        }

        private async void NewClassDiagramHandler()
        {
            if (_selectedFolder is null)
            {
                return;
            }

            string? nf = GetNewFile(".class.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewClassDiagram(nf, Path.GetFileNameWithoutExtension(nf));


                _selectedFolder.Children.Insert(0, new TreeViewModel(_selectedFolder, nf, GetIcon(nf)));
            }

        }

        private async Task NewComponentDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLComponentDiagram(title, fileName, null);

            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";
            var d = new ComponentDiagramDocumentModel(Configuration, _ioService, model, fileName, title, content);

            Documents.ComponentDiagrams.Add(model);
            await AfterCreate(d);
        }

        private async void NewComponentDiagramHandler()
        {
            string? nf = GetNewFile(".component.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewComponentDiagram(nf, Path.GetFileNameWithoutExtension(nf));


                _selectedFolder?.Children.Insert(0, new TreeViewModel(_selectedFolder, nf, GetIcon(nf)));
            }
        }

        private async Task NewSequenceDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLSequenceDiagram(title, fileName);
            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";

            var d = new SequenceDiagramDocumentModel(Configuration, _ioService, model, Documents.ClassDocuments, fileName, title, content);

            Documents.SequenceDiagrams.Add(model);
            await AfterCreate(d);
        }

        private async void NewSequenceDiagramHandler()
        {
            string? nf = GetNewFile(".seq.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewSequenceDiagram(nf, Path.GetFileNameWithoutExtension(nf));


                _selectedFolder?.Children.Insert(0, new TreeViewModel(_selectedFolder, nf, GetIcon(nf)));
            }
        }

        private async void NewUnknownDiagramHandler()
        {
            string? nf = GetNewFile(".puml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewUnknownUMLDiagram(nf, Path.GetFileNameWithoutExtension(nf));


                _selectedFolder?.Children.Insert(0, new TreeViewModel(_selectedFolder, nf, GetIcon(nf)));
            }
        }

        private async void NewYAMLDocumentHandler()
        {
            string? nf = GetNewFile(".yml");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewYAMLDocument(nf, Path.GetFileNameWithoutExtension(nf));


                _selectedFolder?.Children.Insert(0, new TreeViewModel(_selectedFolder, nf, GetIcon(nf)));
            }
        }

        private async Task NewYAMLDocument(string filePath, string fileName)
        {

            var d = new YMLDocumentModel(
            Configuration, _ioService, filePath, fileName, string.Empty);

            await AfterCreate(d);
        }

        private async void NewMarkDownDocumentHandler()
        {
            string? nf = GetNewFile(".md");

            if (!string.IsNullOrEmpty(nf))
            {
                await NewMarkDownDocument(nf, Path.GetFileNameWithoutExtension(nf));


                _selectedFolder?.Children.Insert(0, new TreeViewModel(_selectedFolder, nf, GetIcon(nf)));
            }
        }

        private async Task NewMarkDownDocument(string filePath, string fileName)
        {

            var d = new MDDocumentModel(
            Configuration, _ioService, filePath, fileName, string.Empty);

            await AfterCreate(d);
        }

        private async Task NewUnknownUMLDiagram(string fileName, string title)
        {
            var model = new UMLModels.UMLUnknownDiagram(title, fileName);
            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";
            var d = new UnknownDocumentModel((old, @new) =>
            {
            },
            Configuration, _ioService, model, Documents, fileName, title, content);

            await AfterCreate(d);
        }

        private async Task<TextDocumentModel> OpenClassDiagram(string fileName,
            UMLClassDiagram diagram, int lineNumber, string? searchText = null)
        {
            var content = await File.ReadAllTextAsync(fileName);
            var d = new ClassDiagramDocumentModel(Configuration,
                _ioService, diagram, Documents.ClassDocuments, fileName, diagram.Title, content);

            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }

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
            {
                OpenDocuments.Add(d);
            }

            d.GotoLineNumber(lineNumber, searchText);

            CurrentDocument = d;
        }

        private async Task<bool> CanContinueWithDirtyWrites()
        {
            if (OpenDocuments.Any(z => z.IsDirty))
            {
                ConfirmOpen = true;
                if (_continueClosinTaskSource is not null)
                {
                    await _continueClosinTaskSource.Task;
                }

                if (!AllowContinue)
                {
                    SelectedMRUFolder = _folderBase;
                    return false;
                }
            }
            return true;
        }

        private async Task OpenDirectoryHandler(bool? useAppSettings = false, string? folder = null)
        {
            await _checkMessagesRunning.WaitAsync();
            try
            {

                if (!await CanContinueWithDirtyWrites())
                {
                    return;
                }

                var oldFolder = _folderBase;

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
                {
                    _folderBase = oldFolder;
                    return;
                }

                SelectedMRUFolder = dir;
                lock (_docLock)
                {
                    foreach (var d in OpenDocuments)
                    {
                        d.Close();
                    }

                    if (OpenDocuments.Count > 0)
                    {
                        OpenDocuments.Clear();
                    }
                }
                AppSettings.Default.WorkingDir = dir;
                AppSettings.Default.Save();

                CreateNewComponentDiagram.RaiseCanExecuteChanged();
                CreateNewClassDiagram.RaiseCanExecuteChanged();
                CreateNewSequenceDiagram.RaiseCanExecuteChanged();
                SaveAllCommand.RaiseCanExecuteChanged();
                ScanAllFiles.RaiseCanExecuteChanged();
                GitCommitAndSyncCommand.RaiseCanExecuteChanged();

                await ScanDirectory(dir);

                if (!MRUFolders.Contains(dir))
                {
                    MRUFolders.Add(dir);
                }

                AppSettings.Default.MRU = JsonConvert.SerializeObject(MRUFolders);

                AppSettings.Default.Save();
            }
            finally
            {
                _checkMessagesRunning.Release();
            }

            _messageChecker.Change(1000, Timeout.Infinite);
        }

        private void OpenDocuments_CollectionChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var files = JsonConvert.DeserializeObject<List<string>>(AppSettings.Default.Files);
            if (files == null)
            {
                files = new List<string>();
            }

            if (e.NewItems != null)
            {
                if (!files.Any(p => e.NewItems.Contains(p)))
                {
                    var dm = (BaseDocumentModel?)e.NewItems[0];
                    if (dm != null)
                    {
                        files.Add(dm.FileName);
                    }
                }
            }
            if (e.OldItems != null)
            {
                var dm = (BaseDocumentModel?)e.OldItems[0];
                if (dm != null)
                {
                    files.RemoveAll(p => string.CompareOrdinal(p, dm.FileName) == 0);
                }
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                files.Clear();
            }
            AppSettings.Default.Files = JsonConvert.SerializeObject(files);
            AppSettings.Default.Save();
        }

        private void OpenExplorerHandler()
        {
            if (string.IsNullOrEmpty(_folderBase))
            {
                return;
            }

            ProcessStartInfo psi = new()
            {
                UseShellExecute = true,
                WorkingDirectory = _folderBase,
                FileName = _folderBase
            };
            Process.Start(psi);
        }

        private async Task OpenSequenceDiagram(string fileName, UMLSequenceDiagram diagram,
            int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fileName);

            var d = new SequenceDiagramDocumentModel(Configuration, _ioService, diagram, Documents.ClassDocuments, fileName, diagram.Title, content);

            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }

            d.GotoLineNumber(lineNumber, searchText);

            CurrentDocument = d;
        }

        private void OpenTerminalHandler()
        {
            if (string.IsNullOrEmpty(_folderBase))
            {
                return;
            }

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

        private async Task OpenUnknownDiagram(string fullPath, UMLUnknownDiagram diagram)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            var d = new UnknownDocumentModel((old, @new) =>
            {
            }, Configuration, _ioService, diagram, Documents, fullPath, diagram.Title, content);

            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }

            CurrentDocument = d;
        }

        private void ProcessDataTypes()
        {
            var dt = DataTypes.ToList();

            var dataTypes = (from o in Documents.ClassDocuments
                             from z in o.DataTypes
                             where z is not UMLOther
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
            {
                if (!dt.Contains(r))
                {
                    dt.Add(r);
                    isDirty = true;
                }
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

        private async Task SaveAll()
        {
            if (string.IsNullOrEmpty(_metaDataFile))
            {
                return;
            }

            List<TextDocumentModel> c = new();

            lock (_docLock)
            {
                c = OpenDocuments.OfType<TextDocumentModel>().Where(p => p.IsDirty).ToList();
            }

            foreach (var file in c)
            {
                await Save(file);
            }

            await UpdateDiagramDependencies();

            await ScanAllFilesHandler();
        }

        private async void SaveAllHandler()
        {
            await SaveAll();
        }

        private async void SaveCommandHandler(BaseDocumentModel doc)
        {
            if (doc is not TextDocumentModel textDocumentModel)
            {
                return;
            }
            await Save(textDocumentModel as TextDocumentModel);

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
            {
                return;
            }

            List<string> potentialSequenceDiagrams = new();
            await ScanForFiles(folder, potentialSequenceDiagrams);

            foreach (var seq in potentialSequenceDiagrams)
            {
                await UMLDiagramTypeDiscovery.TryCreateSequenceDiagram(Documents, seq);
            }

            ProcessDataTypes();

            foreach (var doc in OpenDocuments.OfType<SequenceDiagramDocumentModel>())
            {
                doc.UpdateDiagram(Documents.ClassDocuments);
            }

            CurrentActionExecuting = null;
        }

        private async Task ScanDirectory(string? dir)
        {
            if (dir == null)
            {
                return;
            }



            Folder.Children.Clear();

            var start = new FolderTreeViewModel(Folder, dir, true);

            Folder.Children.Add(start);

            await AddFolderItems(dir, start);

            CurrentActionExecuting = "Folder reading complete. Scanning for puml files.";

            await ScanAllFilesHandler();

            CurrentActionExecuting = null;
        }

        private async Task ScanForFiles(string folder, List<string> potentialSequenceDiagrams)
        {
            if ((_cancelCurrentExecutingAction?.IsCancellationRequested).GetValueOrDefault())
            {
                return;
            }

            CurrentActionExecuting = $"Scanning {folder}";

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

        private void SelectDocumentHandler(BaseDocumentModel model)
        {
            CurrentDocument = model;
        }

        private async Task UpdateDiagramDependencies()
        {
            TextDocumentModel[] dm = GetTextDocumentModelReadingArray();

            List<(UMLDiagram, UMLDiagram)> list = new();

            await UpdateDiagrams<ClassDiagramDocumentModel, UMLClassDiagram>(dm, Documents.ClassDocuments);
            await UpdateDiagrams<SequenceDiagramDocumentModel, UMLSequenceDiagram>(dm, Documents.SequenceDiagrams);
            await UpdateDiagrams<ComponentDiagramDocumentModel, UMLComponentDiagram>(dm, Documents.ComponentDiagrams);

            var docs = Application.Current.Dispatcher.Invoke(() =>
            {
                ProcessDataTypes();
                lock (_docLock)
                {
                    return OpenDocuments.OfType<SequenceDiagramDocumentModel>().ToArray();
                }
            });

            foreach (var document in docs)
            {
                document.UpdateDiagram(Documents.ClassDocuments);
            }

            await _documentCollectionSerialization.Save(Documents, _metaDataFile);
        }

        public bool CanClose
        {
            get;
            private set;
        }

        internal async Task ShouldAbortCloseAll()
        {
            if (!await CanContinueWithDirtyWrites())
            {
                CanClose = false;
                return;
            }

            TextDocumentModel[] dm = GetTextDocumentModelReadingArray();
            foreach (var item in dm)
            {


                item.TryClosePreview();
            }

            CanClose = true;

            _ = Application.Current.Dispatcher.InvokeAsync(() => _window.Close());




        }



        public async void GotoDataType(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var lb = (DateTypeRecord?)e.AddedItems[0];
                if (lb != null)
                {
                    await AttemptOpeningFile(lb.FileName, lb.DataType.LineNumber);
                }
            }
        }

        public async void LoadedUI()
        {
            await OpenDirectoryHandler(true);

            var files = JsonConvert.DeserializeObject<List<string>>(AppSettings.Default.Files);
            if (files == null)
            {
                files = new List<string>();
            }

            foreach (var file in files)
            {
                if (File.Exists(file))
                {
                    await AttemptOpeningFile(file);
                }
            }
        }

        public async void TreeItemClickedButtonDown(object sender, MouseButtonEventArgs e)
        {



            if (e.ClickCount == 1)
            {
                return;
            }

            e.Handled = true;
            if (e.Source is FrameworkElement frameworkElement)
            {
                TreeViewModel? model = frameworkElement.DataContext as TreeViewModel;
                if (model is not null)
                {
                    await AttemptOpeningFile(model.FullPath);
                }
            }




        }
        public void TreeItemClickedButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (e.Source is FrameworkElement frameworkElement)
            {
                TreeViewModel? model = frameworkElement.DataContext as TreeViewModel;
                if (model is not null)
                {
                    if (model.IsFile)
                    {
                        _selectedFile = model;
                    }
                    else
                    {
                        _selectedFolder = model;
                    }

                    CreateUMLImage.RaiseCanExecuteChanged();

                }
            }







        }

        public void UISizeChanged()
        {
            AppSettings.Default.WindowWidth = WindowWidth;
            AppSettings.Default.WindowHeight = WindowHeight;
            AppSettings.Default.WindowTop = WindowTop;
            AppSettings.Default.WindowLeft = WindowLeft;
            AppSettings.Default.Save();
        }



    }
}