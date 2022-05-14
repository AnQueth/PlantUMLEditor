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
using System.Windows.Media;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class MainModel : BindingBase
    {
        public record DateTypeRecord(string FileName, UMLDataType DataType);

        private const string DATAJSON = "data.json";
        private const string WILDCARD = "*";
        private const string DEFAULTSCLASS = "defaults.class";
        private readonly SemaphoreSlim _checkMessagesRunning = new(1, 1);
        private readonly ObservableCollection<DateTypeRecord> _dataTypes = new();
        private readonly object _docLock = new();
        private readonly IUMLDocumentCollectionSerialization _documentCollectionSerialization;
        private readonly DelegateCommand<string> _gotoDefinitionCommand;
        private readonly DelegateCommand<string> _findAllReferencesCommand;
        private readonly Lazy<GridSettings> _gridSettingLoader;
        private readonly IIOService _ioService;
        private readonly Timer _messageChecker;

        private readonly MainWindow _window;
        private bool _AllowContinue;

        private CancellationTokenSource? _cancelCurrentExecutingAction;

        private bool _confirmOpen;

        private TaskCompletionSource? _continueClosinTaskSource;
        private string? _currentActionExecuting;

        private string? _folderBase;

        private string? _gitMessages;
        private string _metaDataDirectory = "";

        private string _metaDataFile = "";

        private TreeViewModel? _selectedFile;
        private GlobalFindResult? _selectedFindResult;

        private TreeViewModel? _selectedFolder;
        private string? _selectedMRUFolder;

        private BaseDocumentModel? currentDocument;

        private UMLModels.UMLDocumentCollection documents;

        private TreeViewModel folder;

        private DocumentMessage? selectedMessage;

        private readonly AutoResetEvent _messageCheckerTrigger = new(true);

        public MainModel(IIOService openDirectoryService, IUMLDocumentCollectionSerialization documentCollectionSerialization, MainWindow mainWindow)
        {
            _window = mainWindow;
            CancelExecutingAction = new DelegateCommand(CancelCurrentExecutingAction, () =>
            {
                return _cancelCurrentExecutingAction != null;
            });

            _gotoDefinitionCommand = new DelegateCommand<string>(GotoDefinitionInvoked);
            _findAllReferencesCommand = new DelegateCommand<string>(FindAllReferencesInvoked);
            documents = new UMLModels.UMLDocumentCollection();

            _ioService = openDirectoryService;
            OpenDirectoryCommand = new DelegateCommand(() => _ = OpenDirectoryHandler());
            DeleteMRUCommand = new DelegateCommand<string>((s) => DeleteMRUCommandHandler(s));

            SaveAllCommand = new DelegateCommand(SaveAllHandler, () => !string.IsNullOrEmpty(_folderBase));
            folder = new FolderTreeViewModel(null, Path.GetTempPath(), true);
            _documentCollectionSerialization = documentCollectionSerialization;
            OpenDocuments = new ObservableCollection<BaseDocumentModel>();
            CreateNewJSONDocumentCommand = new DelegateCommand(NewJsonDiagramHandler, () => !string.IsNullOrEmpty(_folderBase));
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

            _ = Task.Run(CheckMessages);

            _gridSettingLoader = new Lazy<GridSettings>(() =>
            {
                GridSettings? l = !string.IsNullOrEmpty(AppSettings.Default.GridSettings) ?
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

        private void DeleteMRUCommandHandler(string mru)
        {
            MRUFolders.Remove(mru);
            SaveMRU();
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

        public bool CanClose
        {
            get;
            private set;
        }

        public DelegateCommand<BaseDocumentModel> CloseDocument
        {
            get;
        }

        public DelegateCommand<BaseDocumentModel> CloseDocumentAndSave
        {
            get;
        }

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

        public DelegateCommand CreateMarkDownDocument
        {
            get;
        }

        public DelegateCommand CreateNewClassDiagram
        {
            get;
        }

        public DelegateCommand CreateNewComponentDiagram
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

        public DelegateCommand CreateYAMLDocument
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

                _messageCheckerTrigger.Set();
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

        public DelegateCommand GitCommitAndSyncCommand
        {
            get;
        }

        public string? GitMessages

        {
            get => _gitMessages;
            set => SetValue(ref _gitMessages, value);
        }

        public ObservableCollection<GlobalFindResult> FindReferenceResults
        {
            get;
        } = new();

        public ObservableCollection<GlobalFindResult> GlobalFindResults { get; } = new ObservableCollection<GlobalFindResult>();

        public DelegateCommand<string> GlobalSearchCommand
        {
            get;
        }
        public DelegateCommand<string> FindAllReferencesCommand => _findAllReferencesCommand;
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
        public DelegateCommand<string> DeleteMRUCommand
        {
            get;
        }
        public ObservableCollection<BaseDocumentModel> OpenDocuments
        {
            get;
        }
        public DelegateCommand CreateNewJSONDocumentCommand
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

        private AppConfiguration Configuration
        {
            get;
        }

        public async void GotoDataType(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                DateTypeRecord? lb = (DateTypeRecord?)e.AddedItems[0];
                if (lb != null)
                {
                    await AttemptOpeningFile(lb.FileName, lb.DataType.LineNumber);
                }
            }
        }

        public async void LoadedUI()
        {
            await OpenDirectoryHandler(true);

            List<string>? files = JsonConvert.DeserializeObject<List<string>>(AppSettings.Default.Files);
            if (files == null)
            {
                files = new List<string>();
            }

            foreach (string? file in files)
            {
                if (File.Exists(file))
                {
                    await AttemptOpeningFile(file);
                }
            }

            _messageCheckerTrigger.Set();
        }

        public void TextDragEnter(object sender, DragEventArgs e)
        {
        }

        public void TextDragLeave(object sender, DragEventArgs e)
        {
        }

        public void TextDragOver(object sender, DragEventArgs e)
        {
            if (currentDocument is not null)
            {
                if (string.Equals(Path.GetExtension(currentDocument.FileName), ".md", StringComparison.OrdinalIgnoreCase))
                {
                    e.Effects = DragDropEffects.Link;
                    e.Handled = true;
                }
            }
            e.Handled = true;
        }

        public void TextDrop(object sender, DragEventArgs e)
        {
            if (currentDocument is not null)
            {
                if (string.Equals(Path.GetExtension(currentDocument.FileName), ".md", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = (string)e.Data.GetData(DataFormats.StringFormat);
                    if (currentDocument is TextDocumentModel tdm)
                    {
                        string name = Path.GetFileNameWithoutExtension(fileName);
                        fileName = Path.GetRelativePath(Path.GetDirectoryName(currentDocument.FileName), fileName);
                        string extension = Path.GetExtension(fileName);

                        fileName = fileName.Replace('\\', '/');

                        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
                        {

                            string imageMD = $"[![{name}]({fileName})]({fileName})";
                            e.Data.SetData(DataFormats.StringFormat, imageMD);
                        }
                        else
                        {
                            string imageMD = $"[{name}]({fileName})";
                            e.Data.SetData(DataFormats.StringFormat, imageMD);

                        }
                    }

                }
            }
        }


        public void TreeDragOver(object sender, DragEventArgs e)
        {



            FrameworkElement? item = e.Source as FrameworkElement;


            if (item is not null)
            {
                FolderTreeViewModel? model = GetItemAtLocation<FolderTreeViewModel>(item, e.GetPosition(item));
                if (model is not null)
                {

                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;

                }

            }

        }

        private T? GetItemAtLocation<T>(FrameworkElement el, Point location)
        {

            HitTestResult hitTestResults = VisualTreeHelper.HitTest(el, location);

            if (hitTestResults is not null && hitTestResults.VisualHit is FrameworkElement)
            {
                if (hitTestResults.VisualHit is FrameworkElement fe)
                {
                    object dataObject = fe.DataContext;


                    if (dataObject is T t)
                    {
                        return t;
                    }
                }

            }

            return default(T);
        }

        public async void TreeDrop(object sender, DragEventArgs e)
        {
            FrameworkElement? item = e.Source as FrameworkElement;


            if (item is not null)
            {
                FolderTreeViewModel? model = GetItemAtLocation<FolderTreeViewModel>(item, e.GetPosition(item));
                if (model is not null)
                {
                    if (!model.IsFile)
                    {
                        string oldFile = (string)e.Data.GetData(DataFormats.StringFormat);
                        string newPath = Path.Combine(model.FullPath, Path.GetFileName(oldFile));

                        try
                        {

                            File.Move(oldFile, newPath);

                            e.Handled = true;
                            await ScanDirectory(this._folderBase);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }





                    }
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

        public void TreeMouseMove(object sender, MouseEventArgs e)
        {
            if (e.MouseDevice.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (e.Source is FrameworkElement fe)
            {
                if (fe.DataContext is TreeViewModel tvm)
                {
                    if (tvm.IsFile)
                    {
                        DragDrop.DoDragDrop(fe, tvm.FullPath, DragDropEffects.Link | DragDropEffects.Move);
                    }
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

        internal async Task ShouldAbortCloseAll()
        {
            if (!await CanContinueWithDirtyWrites())
            {
                CanClose = false;
                return;
            }

            TextDocumentModel[] dm = GetTextDocumentModelReadingArray();
            foreach (TextDocumentModel? item in dm)
            {
                item.TryClosePreview();
            }

            CanClose = true;

            _ = Application.Current.Dispatcher.InvokeAsync(() => _window.Close());
        }

        private static async Task Save(TextDocumentModel doc)
        {
            await doc.Save();
        }

        private static async Task UpdateDiagrams<T1, T2>(TextDocumentModel[] documentModels, LockedList<T2> classDocuments) where T1 : TextDocumentModel where T2 : UMLDiagram
        {
            foreach (T1? document in documentModels.OfType<T1>())
            {
                UMLDiagram? e = await document.GetEditedDiagram();
                if (e == null)
                {
                    continue;
                }

                e.FileName = document.FileName;

                if (e is T2 cd)
                {
                    foreach (T2? oldCd in classDocuments)
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

            foreach (string? file in Directory.EnumerateFiles(dir))
            {
                string p = Path.GetExtension(file);
                if (FileExtension.MD.Compare(p) ||
                    FileExtension.JPG.Compare(p) ||
                    FileExtension.PNG.Compare(p) ||
                    FileExtension.PUML.Compare(p) ||
                    FileExtension.YML.Compare(p)
                    )
                {
                    model.Children.Add(new TreeViewModel(model, file, GetIcon(file)));
                }
            }

            FoldersStatusPersistance? fp = new FoldersStatusPersistance();
            HashSet<string>? closed = fp.GetClosedFolders();

            foreach (string? item in Directory.EnumerateDirectories(dir))
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

                FolderTreeViewModel? fm = new FolderTreeViewModel(model, item, isExpanded);
                model.Children.Add(fm);

                await AddFolderItems(item, fm);
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
            string ext = Path.GetExtension(fullPath);

            if (FileExtension.PUML.Compare(ext))
            {
                (UMLClassDiagram? cd, UMLSequenceDiagram? sd, UMLComponentDiagram? comd, UMLUnknownDiagram? ud) =
                    await UMLDiagramTypeDiscovery.TryFindOrAddDocument(Documents, fullPath);

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
            else if (FileExtension.MD.Compare(ext))
            {
                await OpenMarkDownFile(fullPath, lineNumber, searchText);
            }
            else if (FileExtension.YML.Compare(ext))
            {
                await OpenYMLFile(fullPath, lineNumber, searchText);
            }
            else if (FileExtension.JPG.Compare(ext) || FileExtension.PNG.Compare(ext))
            {
                await OpenImageFile(fullPath);
            }
        }

        private void CancelCurrentExecutingAction()
        {
            _cancelCurrentExecutingAction?.Cancel();
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

        private async void CheckMessages()
        {
            while (true)
            {
                await Task.Delay(500);

                _messageCheckerTrigger.WaitOne();

                await _checkMessagesRunning.WaitAsync();


                try
                {
                    if (string.IsNullOrEmpty(_metaDataFile) || string.IsNullOrEmpty(_folderBase))
                    {

                        continue;
                    }

                    if (Application.Current == null)
                    {
                        continue;
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

                        foreach (TextDocumentModel? doc in dm)
                        {
                            UMLDiagram? d = await doc.GetEditedDiagram();
                            if (d != null)
                            {
                                d.FileName = doc.FileName;
                                diagrams.Add(d);
                            }
                        }

                        foreach (UMLClassDiagram? doc in Documents.ClassDocuments)
                        {
                            if (!dm.Any(p => p.FileName == doc.FileName))
                            {
                                diagrams.Add(doc);
                            }
                        }
                        foreach (UMLComponentDiagram? doc in Documents.ComponentDiagrams)
                        {
                            if (!dm.Any(p => p.FileName == doc.FileName))
                            {
                                diagrams.Add(doc);
                            }
                        }
                        foreach (UMLSequenceDiagram? doc in Documents.SequenceDiagrams)
                        {
                            if (!dm.Any(p => p.FileName == doc.FileName))
                            {
                                diagrams.Add(doc);
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    DocumentMessageGenerator documentMessageGenerator = new(diagrams);
                    List<DocumentMessage>? newMessages = documentMessageGenerator.Generate(_folderBase);

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        List<DocumentMessage> removals = new();
                        foreach (DocumentMessage? item in Messages)
                        {
                            if (!newMessages.Any(z => string.CompareOrdinal(z.FileName, item.FileName) == 0 &&
                            string.CompareOrdinal(z.Text, item.Text) == 0 && z.LineNumber == item.LineNumber))
                            {
                                removals.Add(item);
                            }
                        }

                        removals.ForEach(p => Messages.Remove(p));

                        foreach (DocumentMessage? item in newMessages)
                        {
                            if (!Messages.Any(z => string.CompareOrdinal(z.FileName, item.FileName) == 0 &&
                           string.CompareOrdinal(z.Text, item.Text) == 0 && z.LineNumber == item.LineNumber))
                            {
                                Messages.Add(item);
                            }
                        }

                        foreach (DocumentMessage? d in Messages)
                        {
                            if ((d is MissingMethodDocumentMessage || d is MissingDataTypeMessage) && d.FixingCommand is null)
                            {
                                d.FixingCommand = new DelegateCommand<DocumentMessage>(FixingCommandHandler);
                            }

                            lock (_docLock)
                            {
                                IEnumerable<BaseDocumentModel>? docs = OpenDocuments.Where(p => string.Equals(p.FileName, d.FileName, StringComparison.Ordinal));
                                foreach (BaseDocumentModel? doc in docs)
                                {
                                    if (CurrentDocument == doc && doc is TextDocumentModel textDoc)
                                    {
                                        textDoc.ReportMessage(d);
                                    }
                                }
                            }
                        }


                    });
                }
                finally
                {
                    _checkMessagesRunning.Release();
                }

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

            TreeViewModel? folder = FindFolderContaining(Folder, _selectedFile.FullPath);

            PlantUMLImageGenerator.UMLImageCreateRecord? res = await generator.Create();

            if (folder != null)
            {
                TreeViewModel? file = folder.Children.First(z => string.Equals(z.FullPath, _selectedFile.FullPath, StringComparison.Ordinal));

                int ix = folder.Children.IndexOf(file);
                if (!folder.Children.Any(z => z.FullPath == res.fileName))
                {
                    folder.Children.Insert(ix, new TreeViewModel(folder, res.fileName, GetIcon(res.fileName)));
                }
            }
        }

        private TreeViewModel? FindFolderContaining(TreeViewModel root, string selectedFile)
        {
            foreach (TreeViewModel? f in root.Children)
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
                    TreeViewModel? g = FindFolderContaining(f, selectedFile);
                    if (g != null)
                    {
                        return g;
                    }
                }
            }

            return null;
        }

        private async void FixingCommandHandler(DocumentMessage sender)
        {
            TextDocumentModel[] textDocumentModels = GetTextDocumentModelReadingArray();

            switch (sender)
            {
                case MissingMethodDocumentMessage missingMethodMessage:
                    await AddMissingAttributeToClass(textDocumentModels, missingMethodMessage);
                    break;
                case MissingDataTypeMessage missingDataTypeMessage:
                    await AddDefaultDataType(textDocumentModels, missingDataTypeMessage);
                    break;

            }


        }

        private async Task AddMissingAttributeToClass(TextDocumentModel[] textDocumentModels, MissingMethodDocumentMessage missingMethodMessage)
        {
            foreach (UMLClassDiagram? doc in Documents.ClassDocuments)
            {
                UMLDataType? d = doc.DataTypes.FirstOrDefault(p => p.Id == missingMethodMessage.MissingMethodDataTypeId);
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

                ClassDiagramDocumentModel? od = textDocumentModels.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => string.CompareOrdinal(p.FileName, doc.FileName) == 0);
                if (od != null)
                {
                    CurrentDocument = od;
                    od.UpdateDiagram(doc);
                }
                else
                {
                    od = await OpenClassDiagram(doc.FileName, doc, 0, null) as ClassDiagramDocumentModel;
                    if (od is not null)
                    {

                        CurrentDocument = od;
                        od.UpdateDiagram(doc);
                    }
                }
            }
        }

        private async Task AddDefaultDataType(TextDocumentModel[] textDocumentModels, MissingDataTypeMessage missingDataTypeMessage)
        {
            UMLClassDiagram? f = Documents.ClassDocuments.FirstOrDefault(p => string.CompareOrdinal(p.Title, DEFAULTSCLASS) == 0);
            if (f != null)
            {
                f.Package.Children.Add(new UMLClass("", "default", false, missingDataTypeMessage.MissingDataTypeName, new List<UMLDataType>()));




                ClassDiagramDocumentModel? od = textDocumentModels.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => p.FileName == f.FileName);
                if (od != null)
                {
                    CurrentDocument = od;
                    od.UpdateDiagram(f);
                }
                else
                {

                    od = await OpenClassDiagram(f.FileName, f, 0, null) as ClassDiagramDocumentModel;


                    if (od != null)
                    {
                        CurrentDocument = od;
                        od.UpdateDiagram(f);
                    }
                }
            }
            else
            {
                MessageBox.Show("Create a defaults.class document in the root of the work folder first.");
            }
        }

        private string? GetIcon(string file)
        {
            string ext = Path.GetExtension(file);
            if (file.Contains(".component.puml", StringComparison.OrdinalIgnoreCase))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/com.png";
            }
            else if (file.Contains(".class.puml", StringComparison.OrdinalIgnoreCase))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/class.png";
            }
            else if (file.Contains(".seq.puml", StringComparison.OrdinalIgnoreCase))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/sequence.png";
            }
            else if (FileExtension.MD.Compare(ext))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/md.png";
            }
            else if (FileExtension.YML.Compare(ext))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/yml.png";
            }
            else if ((FileExtension.PNG.Compare(ext)) || (FileExtension.JPG.Compare(ext)))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/emblem_512.png";
            }
            else if (FileExtension.PUML.Compare(ext))
            {
                return @"pack://application:,,,/PlantUMLEditor;component/images/uml.png";
            }
            return null;
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

        private TextDocumentModel[] GetTextDocumentModelReadingArray()
        {
            TextDocumentModel[] dm;
            lock (_docLock)
            {
                dm = OpenDocuments.OfType<TextDocumentModel>().ToArray();
            }

            return dm;
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

            _metaDataFile = Path.Combine(_metaDataDirectory, DATAJSON);
            return _folderBase;
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

        private async void GlobalSearchHandler(string obj)
        {
            if (string.IsNullOrEmpty(_folderBase))
            {
                return;
            }

            List<GlobalFindResult>? findresults = await GlobalSearch.Find(_folderBase, obj, new string[]
            {WILDCARD + FileExtension.PUML.Extension, WILDCARD + FileExtension.MD.Extension, WILDCARD + FileExtension.YML.Extension
            });
            GlobalFindResults.Clear();
            foreach (GlobalFindResult? f in findresults)
            {
                GlobalFindResults.Add(f);
            }
        }

        private void FindAllReferencesInvoked(string text)
        {
            FindReferenceResults.Clear();

            List<UMLDataType> dataTypes = new();

            foreach (UMLDataType? fdt in Documents.ClassDocuments.SelectMany(z => z.DataTypes))
            {
                dataTypes.Add(fdt);
            }



            foreach (var item in Documents.ClassDocuments.Where(z =>
         z.DataTypes.Any(v => string.CompareOrdinal(v.NonGenericName, text) == 0)).Select(p => new
         {
             FN = p.FileName,
             D = p,
             DT = p.DataTypes.First(z => z.NonGenericName == text)
         }))
            {
                FindReferenceResults.Add(new GlobalFindResult(item.FN, item.DT.LineNumber, item.DT.Name, text));


                foreach (GlobalFindResult? ln in Documents.ClassDocuments.SelectMany(z => z.DataTypes.SelectMany(x => x.Properties.Where(g =>
                DocumentMessageGenerator.GetCleanTypes(dataTypes, g.ObjectType.Name).Contains(item.DT.NonGenericName))
                .Select(g => new GlobalFindResult(z.FileName, x.LineNumber, g.Signature, text)))))
                {
                    FindReferenceResults.Add(ln);
                }
                foreach (GlobalFindResult? ln in Documents.ClassDocuments.SelectMany(z => z.DataTypes.SelectMany(x => x.Methods.SelectMany(k => k.Parameters.Where(g =>
                  DocumentMessageGenerator.GetCleanTypes(dataTypes, g.ObjectType.Name).Contains(item.DT.NonGenericName))
              .Select(g => new GlobalFindResult(z.FileName, x.LineNumber, k.Signature, text))))))
                {
                    FindReferenceResults.Add(ln);
                }
                foreach (GlobalFindResult? ln in Documents.ClassDocuments.SelectMany(z => z.DataTypes.SelectMany(x => x.Methods.Where(k =>
                DocumentMessageGenerator.GetCleanTypes(dataTypes, k.ReturnType.Name).Contains(item.DT.NonGenericName))
           .Select(g => new GlobalFindResult(z.FileName, x.LineNumber, g.Signature, text)))))
                {
                    FindReferenceResults.Add(ln);
                }

                foreach (GlobalFindResult? ln in Documents.SequenceDiagrams.SelectMany(z => z.LifeLines.Where(x => x.DataTypeId == item.DT.NonGenericName).Select(c =>
                new GlobalFindResult(z.FileName, c.LineNumber, c.Text, text))))

                {
                    FindReferenceResults.Add(ln);
                }
                foreach (GlobalFindResult? ln in Documents.SequenceDiagrams.SelectMany(z => z.Entities.OfType<UMLSequenceConnection>()
                .Where(x => x.From?.DataTypeId == item.DT.Id || x.To?.DataTypeId == item.DT.Id).Where(c => c.Action is not null).Select(c =>
               new GlobalFindResult(z.FileName, c.LineNumber, c.Action.Signature, text))))

                {
                    FindReferenceResults.Add(ln);
                }

            }
        }
        private async void GotoDefinitionInvoked(string text)
        {
            foreach (var item in Documents.ClassDocuments.Where(z =>
            z.DataTypes.Any(v => string.CompareOrdinal(v.NonGenericName, text) == 0)).Select(p => new
            {
                FN = p.FileName,
                D = p,
                DT = p.DataTypes.First(z => z.NonGenericName == text)
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
            string[]? mrus = JsonConvert.DeserializeObject<string[]>(AppSettings.Default.MRU ?? "[]");
            if (mrus != null)
            {
                foreach (string? s in mrus)
                {
                    MRUFolders.Add(s);
                }
            }
        }

        private async Task NewClassDiagram(string fileName, string title)
        {
            UMLClassDiagram? model = new UMLModels.UMLClassDiagram(title, fileName, null);
            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";
            ClassDiagramDocumentModel? d = new ClassDiagramDocumentModel(
                Configuration, _ioService, model, Documents.ClassDocuments, fileName, title, content, _messageCheckerTrigger);

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
            UMLComponentDiagram? model = new UMLModels.UMLComponentDiagram(title, fileName, null);

            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";
            ComponentDiagramDocumentModel? d = new ComponentDiagramDocumentModel(Configuration,
                _ioService, model, fileName, title, content, _messageCheckerTrigger);

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

        private async Task NewMarkDownDocument(string filePath, string fileName)
        {
            MDDocumentModel? d = new MDDocumentModel(
            Configuration, _ioService, filePath, fileName, string.Empty, _messageCheckerTrigger);

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

        private async Task NewSequenceDiagram(string fileName, string title)
        {
            UMLSequenceDiagram? model = new UMLModels.UMLSequenceDiagram(title, fileName);
            string content = $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n";

            SequenceDiagramDocumentModel? d = new SequenceDiagramDocumentModel(Configuration,
                _ioService, model, Documents.ClassDocuments, fileName, title, content, _messageCheckerTrigger);

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

        private async void NewJsonDiagramHandler()
        {
            string? nf = GetNewFile(".json.puml");

            if (!string.IsNullOrEmpty(nf))
            {
                string title = Path.GetFileNameWithoutExtension(nf);
                await NewUnknownUMLDiagram(nf, title, $"@startjson\r\ntitle {title}\r\n\r\n@endjson\r\n");

                _selectedFolder?.Children.Insert(0, new TreeViewModel(_selectedFolder, nf, GetIcon(nf)));
            }
        }

        private async void NewUnknownDiagramHandler()
        {
            string? nf = GetNewFile(".puml");

            if (!string.IsNullOrEmpty(nf))
            {
                string title = Path.GetFileNameWithoutExtension(nf);
                await NewUnknownUMLDiagram(nf, title, $"@startuml\r\ntitle {title}\r\n\r\n@enduml\r\n");

                _selectedFolder?.Children.Insert(0, new TreeViewModel(_selectedFolder, nf, GetIcon(nf)));
            }
        }

        private async Task NewUnknownUMLDiagram(string fileName, string title, string content)
        {
            UMLUnknownDiagram? model = new UMLModels.UMLUnknownDiagram(title, fileName);

            UnknownDocumentModel? d = new UnknownDocumentModel((old, @new) =>
            {
            },
            Configuration, _ioService, model, Documents, fileName, title, content, _messageCheckerTrigger);

            await AfterCreate(d);
        }

        private async Task NewYAMLDocument(string filePath, string fileName)
        {
            YMLDocumentModel? d = new YMLDocumentModel(
            Configuration, _ioService, filePath, fileName, string.Empty, _messageCheckerTrigger);

            await AfterCreate(d);
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

        private async Task<TextDocumentModel> OpenClassDiagram(string fileName,
            UMLClassDiagram diagram, int lineNumber, string? searchText = null)
        {
            string? content = await File.ReadAllTextAsync(fileName);
            ClassDiagramDocumentModel? d = new ClassDiagramDocumentModel(Configuration,
                _ioService, diagram, Documents.ClassDocuments, fileName, diagram.Title, content, _messageCheckerTrigger);

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
            ComponentDiagramDocumentModel? d = new ComponentDiagramDocumentModel(Configuration,
                _ioService, diagram, fileName, diagram.Title, content, _messageCheckerTrigger);

            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }

            d.GotoLineNumber(lineNumber, searchText);

            CurrentDocument = d;
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

                string? oldFolder = _folderBase;

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
                    foreach (BaseDocumentModel? d in OpenDocuments)
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

                SortedSet<string>? mru = new SortedSet<string>(MRUFolders);



                if (!mru.Contains(dir))
                {
                    mru.Add(dir);
                }

                string? sf = SelectedMRUFolder;
                MRUFolders.Clear();
                foreach (string? m in mru)
                {
                    MRUFolders.Add(m);
                }

                SelectedMRUFolder = sf;

                SaveMRU();
            }
            finally
            {
                _checkMessagesRunning.Release();
            }

            _messageCheckerTrigger.Set();


        }

        private void SaveMRU()
        {
            AppSettings.Default.MRU = JsonConvert.SerializeObject(MRUFolders);

            AppSettings.Default.Save();
        }

        private void OpenDocuments_CollectionChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            List<string>? files = JsonConvert.DeserializeObject<List<string>>(AppSettings.Default.Files);
            if (files == null)
            {
                files = new List<string>();
            }

            if (e.NewItems != null)
            {
                if (!files.Any(p => e.NewItems.Contains(p)))
                {
                    BaseDocumentModel? dm = (BaseDocumentModel?)e.NewItems[0];
                    if (dm != null)
                    {
                        files.Add(dm.FileName);
                    }
                }
            }
            if (e.OldItems != null)
            {
                BaseDocumentModel? dm = (BaseDocumentModel?)e.OldItems[0];
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

        private async Task OpenImageFile(string fullPath)
        {
            ImageDocumentModel? d = new ImageDocumentModel(
                fullPath,
                Path.GetFileName(fullPath));
            await d.Init();
            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }

            CurrentDocument = d;
        }

        private async Task OpenMarkDownFile(string fullPath, int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            MDDocumentModel? d = new MDDocumentModel(Configuration, _ioService,
                fullPath,
                Path.GetFileName(fullPath)
                , content, _messageCheckerTrigger);

            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }
            d.GotoLineNumber(lineNumber, searchText);
            CurrentDocument = d;
        }

        private async Task OpenSequenceDiagram(string fileName, UMLSequenceDiagram diagram,
            int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fileName);

            SequenceDiagramDocumentModel? d = new SequenceDiagramDocumentModel(Configuration,
                _ioService, diagram, Documents.ClassDocuments, fileName, diagram.Title, content, _messageCheckerTrigger);

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
            UnknownDocumentModel? d = new UnknownDocumentModel((old, @new) =>
            {
            }, Configuration, _ioService, diagram, Documents, fullPath, diagram.Title, content, _messageCheckerTrigger);

            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }

            CurrentDocument = d;
        }

        private async Task OpenYMLFile(string fullPath, int lineNumber, string? searchText)
        {
            string content = await File.ReadAllTextAsync(fullPath);
            YMLDocumentModel? d = new YMLDocumentModel(Configuration, _ioService,
                fullPath,
                Path.GetFileName(fullPath)
                , content, _messageCheckerTrigger);

            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }
            d.GotoLineNumber(lineNumber, searchText);
            CurrentDocument = d;
        }

        private void ProcessDataTypes()
        {
            List<DateTypeRecord>? dt = DataTypes.ToList();

            DateTypeRecord[]? dataTypes = (from o in Documents.ClassDocuments
                                           from z in o.DataTypes
                                           where z is not UMLOther && z is not UMLComment
                                           select new DateTypeRecord(o.FileName, z)).ToArray();

            bool isDirty = false;
            foreach (DateTypeRecord? r in dt.ToArray())
            {
                if (!dataTypes.Contains(r))
                {
                    dt.Remove(r);
                    isDirty = true;
                }
            }

            foreach (DateTypeRecord? r in dataTypes)
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
                foreach (DateTypeRecord? d in dt.OrderBy(z => z.DataType.Namespace).ThenBy(z => z.DataType.Name))
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

            foreach (TextDocumentModel? file in c)
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
            await Save(textDocumentModel);

            await UpdateDiagramDependencies();

            await ScanAllFilesHandler();
        }

        private async Task ScanAllFilesHandler()
        {
            Documents.ClassDocuments.Clear();
            Documents.SequenceDiagrams.Clear();
            Documents.ComponentDiagrams.Clear();

            string? folder = GetWorkingFolder();
            if (folder == null)
            {
                return;
            }

            List<string> potentialSequenceDiagrams = new();
            await ScanForFiles(folder, potentialSequenceDiagrams);

            foreach (string? seq in potentialSequenceDiagrams)
            {
                await UMLDiagramTypeDiscovery.TryCreateSequenceDiagram(Documents, seq);
            }

            ProcessDataTypes();

            foreach (SequenceDiagramDocumentModel? doc in OpenDocuments.OfType<SequenceDiagramDocumentModel>())
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

            FolderTreeViewModel? start = new FolderTreeViewModel(Folder, dir, true);

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

            foreach (string? file in Directory.EnumerateFiles(folder, WILDCARD + FileExtension.PUML.Extension))
            {
                if (null == await UMLDiagramTypeDiscovery.TryCreateClassDiagram(Documents, file))
                {
                    potentialSequenceDiagrams.Add(file);
                }
            }
            foreach (string? file in Directory.EnumerateDirectories(folder))
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

            SequenceDiagramDocumentModel[]? docs = Application.Current.Dispatcher.Invoke(() =>
            {
                ProcessDataTypes();
                lock (_docLock)
                {
                    return OpenDocuments.OfType<SequenceDiagramDocumentModel>().ToArray();
                }
            });

            foreach (SequenceDiagramDocumentModel? document in docs)
            {
                document.UpdateDiagram(Documents.ClassDocuments);
            }

            await _documentCollectionSerialization.Save(Documents, _metaDataFile);
        }
    }
}