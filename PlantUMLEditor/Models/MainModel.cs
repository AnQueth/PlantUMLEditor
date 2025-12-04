using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlantUML;
using PlantUMLEditor.Models.Runners;
using PlantUMLEditorAI;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;

using System.Xml.Linq;
using UMLModels;
using Xceed.Wpf.AvalonDock.Converters;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel : BindingBase
    {
        public record DateTypeRecord(string FileName, UMLDataType DataType);

        private const string DATAJSON = "data.json";
        private const string DEFAULTSCLASS = "defaults.class";
        private const string WILDCARD = "*";
        private readonly SemaphoreSlim _checkMessagesRunning = new(1, 1);
        private readonly ObservableCollection<DateTypeRecord> _dataTypes = new();
        private readonly object _docLock = new();
        private readonly IUMLDocumentCollectionSerialization _documentCollectionSerialization;



        private readonly IIOService _ioService;


        private readonly Channel<bool> _messageCheckerTrigger = Channel.CreateUnbounded<bool>();
        private FileSystemWatcher? _fileWatcher;
        private readonly MainWindow _window;
        private GitStatusMonitor? _gitStatusMonitor;
        private string _currentGitBranch = string.Empty;

        private RunBlocker _closingBlocker = new();

        private bool _confirmOpen;
        private CancellationTokenSource? _cancelCurrentExecutingAction;


        private string? _currentActionExecuting;

        private string? _rootFolder;

        private string? FolderBase
        {
            get => _rootFolder;
            set
            {
                SetValue(ref _rootFolder, value);
                GlobalSearch.RootDirectory = value ?? string.Empty;
            }
        }

        private string? _gitMessages;
        private string _metaDataDirectory = "";

        private string _metaDataFile = "";


        private bool _spellCheck;
        private BaseDocumentModel? _currentDocument;


        private readonly NewFileManager _newFileManager;
        private readonly TemplateStorage _templateStorage;
        private DocumentMessage? selectedMessage;

        public TemplateStorage TemplateStorage
        {
            get => _templateStorage;
        }

        public string CurrentGitBranch
        {
            get => _currentGitBranch;
            set => SetValue(ref _currentGitBranch, value);
        }

        public DelegateCommand<TreeViewModel> UndoGitChangesCommand { get; private set; }
        public MainModel(IIOService openDirectoryService,
            IUMLDocumentCollectionSerialization documentCollectionSerialization,
            MainWindow mainWindow)
        {
            _window = mainWindow;
            CancelExecutingAction = new DelegateCommand(CancelCurrentExecutingAction, () =>
            {
                return _cancelCurrentExecutingAction != null;
            });


            UndoAIEditsCommand = new AsyncDelegateCommand<ObservableCollection<UndoOperation>>(UndoEditCommandHandler);

            GotoDefinitionCommand = new DelegateCommand<string>(GotoDefinitionInvoked);
            FindAllReferencesCommand = new DelegateCommand<string>(FindAllReferencesInvoked);
            Documents = new UMLModels.UMLDocumentCollection();
            SendChatCommand = new AsyncDelegateCommand<BaseDocumentModel>(SendChat,
                (doc) =>
                {
                    return ChatText.Length > 0;
                });
            _ioService = openDirectoryService;
            OpenDirectoryCommand = new DelegateCommand(() => _ = OpenDirectoryHandler());
            DeleteMRUCommand = new DelegateCommand<string>((s) => DeleteMRUCommandHandler(s));
            OpenHelpCommand = new DelegateCommand(OpenHelpCommandHandler);
            SaveAllCommand = new DelegateCommand(SaveAllHandler, () => !string.IsNullOrEmpty(FolderBase));
            UndoGitChangesCommand = new DelegateCommand<TreeViewModel>(UndoGitChangesHandler, (node) => node != null && node.IsFile && node.GitStatus == GitFileStatus.Modified);
            Folder = new FolderTreeViewModel(null, Path.GetTempPath(), true, Statics.GetClosedFolderIcon());
            _documentCollectionSerialization = documentCollectionSerialization;
            OpenDocuments = new ObservableCollection<BaseDocumentModel>();
            CreateNewJSONDocumentCommand = new DelegateCommand(NewJsonDiagramHandler, () => !string.IsNullOrEmpty(FolderBase));
            CreateNewUnknownDiagram = new DelegateCommand(NewUnknownDiagramHandler, () => !string.IsNullOrEmpty(FolderBase));
            OpenUMLColorConfigCommand = new DelegateCommand(OpenUMLColorConfigHandler);
            OpenMDColorConfigCommand = new DelegateCommand(OpenMDColorConfigHandler);
            CreateNewURLLinkCommand = new DelegateCommand(NewURLLinkDiagramHandler, () => !string.IsNullOrEmpty(FolderBase));
            CreateNewSequenceDiagram = new DelegateCommand(NewSequenceDiagramHandler, () => !string.IsNullOrEmpty(FolderBase));
            CreateNewClassDiagram = new DelegateCommand(NewClassDiagramHandler, () => !string.IsNullOrEmpty(FolderBase));
            CreateNewComponentDiagram = new DelegateCommand(NewComponentDiagramHandler, () => !string.IsNullOrEmpty(FolderBase));
            CreateMarkDownDocument = new DelegateCommand(NewMarkDownDocumentHandler, () => !string.IsNullOrEmpty(FolderBase));
            CreateYAMLDocument = new DelegateCommand(NewYAMLDocumentHandler, () => !string.IsNullOrEmpty(FolderBase));
            CreateUMLImage = new DelegateCommand(CreateUMLImageHandler, () => _selectedFile is not null);
            GitCommitAndSyncCommand = new DelegateCommand(GitCommitAndSyncCommandHandler, () => !string.IsNullOrEmpty(FolderBase));
            OpenSettingsCommand = new DelegateCommand(OpenSettingsCommandHandler);
            CloseDocument = new DelegateCommand<BaseDocumentModel>(CloseDocumentHandler);
            CloseDocumentAndSave = new DelegateCommand<BaseDocumentModel>(CloseDocumentAndSaveHandler);
            SaveCommand = new DelegateCommand<BaseDocumentModel>(SaveCommandHandler);
            EditTemplatesCommand = new DelegateCommand(EditTemplatesCommandHandler);
            Messages = new ObservableCollection<DocumentMessage>();
            Messages.CollectionChanged += Messages_CollectionChanged;
            SelectDocumentCommand = new DelegateCommand<BaseDocumentModel>(SelectDocumentHandler);
            GlobalSearchCommand = new DelegateCommand<string>(GlobalSearchHandler);
            ScanAllFiles = new DelegateCommand(async () => await ScanAllFilesHandler(), () => !string.IsNullOrEmpty(FolderBase));
            OpenTerminalCommand = new DelegateCommand(OpenTerminalHandler);
            OpenExplorerCommand = new DelegateCommand(OpenExplorerHandler);
            DocFXServeCommand = new DelegateCommand(DocFXServeCommandHandler, () =>
            {
                return this.FolderBase is not null &&
                DOCFXRunner.FindDocFXConfig(this.FolderBase) != null &&
                File.Exists(AppSettings.Default.DocFXEXE);
            });
            ApplyTemplateCommand = new DelegateCommand(ApplyTemplateCommandHandler,
    () => SelectedTemplate != null && TemplatesEnabled);

            NewChatCommand = new DelegateCommand(NewChatCommandHandler);


            OpenDocuments.CollectionChanged += OpenDocuments_CollectionChanged;

            OpenDocumenntManager = new OpenDocumentManager(OpenDocuments, Documents, _docLock,
                _ioService, _messageCheckerTrigger);

            UIModel = new UISettingsModel();

            _ = Task.Run(CheckMessages);





            _ = new Timer(MRULoader, null, 10, Timeout.Infinite);

            _newFileManager = new NewFileManager(_ioService, AfterCreate,
                Documents, _messageCheckerTrigger);

            _templateStorage = new TemplateStorage();
            
            // Initialize TemplatePath to Documents folder if not set or invalid
            if (string.IsNullOrWhiteSpace(AppSettings.Default.TemplatePath) || 
                !Path.IsPathRooted(AppSettings.Default.TemplatePath))
            {
                AppSettings.Default.TemplatePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "PlantUMLEditor", "Templates");
                AppSettings.Default.Save();
            }
            
            _ = _templateStorage.Load(AppSettings.Default.TemplatePath);

            EditorFontSize = AppSettings.Default.EditorFontSize;
        }



        

        private int _selectedTab;
        public int SelectedToolTab
        {
            get => _selectedTab;
            set => SetValue(ref _selectedTab, value);
        }
        


        public double EditorFontSize
        {
            get => AppSettings.Default.EditorFontSize;
            set
            {

                AppSettings.Default.EditorFontSize = value;
                AppSettings.Default.Save();

                base.PropertyChangedInvoke();


            }

        }


        

        private void OpenMDColorConfigHandler()
        {
            var win = new MDColorCodingConfigWindow();
            win.ShowDialog();
        }

        private void OpenUMLColorConfigHandler()
        {
            var uMLColorCodingConfig = new UMLColorCodingConfigWindow();
            uMLColorCodingConfig.ShowDialog();
        }

        
        public bool AllowContinueClosing
        {
            get;
            set;
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
                    _closingBlocker.Block();
                }
                else if (!value && _confirmOpen)
                {
                    _closingBlocker.Unblock();
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

        public DelegateCommand CreateNewJSONDocumentCommand
        {
            get;
        }

        public DelegateCommand CreateNewSequenceDiagram
        {
            get;
        }

        public DelegateCommand CreateNewURLLinkCommand
        {
            get;
        }

        public DelegateCommand CreateNewUnknownDiagram
        {
            get;
        }
        public DelegateCommand OpenUMLColorConfigCommand { get; }
        public DelegateCommand OpenMDColorConfigCommand { get; }
      
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
            get => _currentDocument;
            set
            {
                if (_currentDocument != null)
                {
                    _currentDocument.Visible = Visibility.Collapsed;

                }



                SetValue(ref _currentDocument, value);
                if (value != null)
                {
                    value.Visible = Visibility.Visible;


                }

                if (_currentDocument is TextDocumentModel)
                {
                    TemplatesEnabled = true;
                }
                else
                {
                    TemplatesEnabled = false;
                }
                ApplyTemplateCommand.RaiseCanExecuteChanged();
                _messageCheckerTrigger.Writer.TryWrite(true);
            }
        }

        public ObservableCollection<DateTypeRecord> DataTypes => _dataTypes;

        public DelegateCommand<string> DeleteMRUCommand
        {
            get;
        }

        public string DocFXExe
        {
            get => AppSettings.Default.DocFXEXE;
            set
            {
                AppSettings.Default.DocFXEXE = value;
                AppSettings.Default.Save();

                DocFXServeCommand.RaiseCanExecuteChanged();
            }
        }

        public DelegateCommand DocFXServeCommand { get; }

        public UMLModels.UMLDocumentCollection Documents
        {
            get;
            init;
        }

        public DelegateCommand<string> FindAllReferencesCommand
        {
            get;
            init;
        }

        public ObservableCollection<GlobalFindResult> FindReferenceResults
        {
            get;
        } = new();

        public TreeViewModel Folder
        {
            get;
            init;
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

        public ObservableCollection<GlobalFindResult> GlobalFindResults { get; } = new ObservableCollection<GlobalFindResult>();

        public DelegateCommand<string> GlobalSearchCommand
        {
            get;
        }

        public DelegateCommand<string> GotoDefinitionCommand
        {
            get;
            init;
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

        public DelegateCommand OpenHelpCommand { get; }

        public DelegateCommand OpenSettingsCommand { get; }

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
        public DelegateCommand EditTemplatesCommand { get; }
        public DelegateCommand ScanAllFiles
        {
            get;
        }

        public DelegateCommand<BaseDocumentModel> SelectDocumentCommand
        {
            get;
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

        public bool SpellCheck
        {
            get => _spellCheck;
            set => SetValue(ref _spellCheck, value);
        }

        private readonly OpenDocumentManager OpenDocumenntManager;

        public UISettingsModel UIModel { get; }
        public DelegateCommand ApplyTemplateCommand { get; }

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

            DocFXServeCommand.RaiseCanExecuteChanged();

            _messageCheckerTrigger.Writer.TryWrite(true);

            // Start Git status monitoring for current folder, if any
            string? folder = GetWorkingFolder();
            if (!string.IsNullOrEmpty(folder))
            {
                _gitStatusMonitor?.Dispose();
                _gitStatusMonitor = new GitStatusMonitor(folder, Application.Current.Dispatcher, ApplyGitStatuses, interval: TimeSpan.FromSeconds(2));
                _gitStatusMonitor.Start();

                var git = new PlantUMLEditor.Models.Runners.GitSupport();
                CurrentGitBranch = git.GetCurrentBranch(folder);
            }
        }

        public void TextDragEnter(object sender, DragEventArgs e)
        {
        }

        public void TextDragLeave(object sender, DragEventArgs e)
        {
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

        

        

        


        

        private void CancelCurrentExecutingAction()
        {
            _cancelCurrentExecutingAction?.Cancel();
        }

        

        

        

        

        

        

        private void DeleteMRUCommandHandler(string mru)
        {
            MRUFolders.Remove(mru);
            SaveMRU();
        }

        

        



        

        

        



        

        

        

        

        


        



        

        



        private void OpenHelpCommandHandler()
        {
            HelpWindow help = new();
            help.ShowDialog();
        }



        private void OpenSettingsCommandHandler()
        {
            SettingsWindow settings = new();
            settings.ShowDialog();
        }

        private void UndoGitChangesHandler(TreeViewModel node)
        {
            if (node == null || !node.IsFile) return;

            string? repoPath = GetWorkingFolder();
            if (string.IsNullOrEmpty(repoPath) )
            {
                return;
            }

            try
            {
                var git = new PlantUMLEditor.Models.Runners.GitSupport();
                if (git.UndoChanges(repoPath, node.FullPath))
                {
                    node.GitStatus = GitFileStatus.Unmodified;
                    // If document is open, reload content from disk and clear dirty flag
                    BaseDocumentModel? open;
                    lock (_docLock)
                    {
                        open = OpenDocuments.FirstOrDefault(d => string.Equals(d.FileName, node.FullPath, StringComparison.Ordinal));
                    }
                    if (open is TextDocumentModel tdm)
                    {
                        string disk = System.IO.File.Exists(node.FullPath) ? System.IO.File.ReadAllText(node.FullPath) : string.Empty;
                        tdm.Content = disk;
                        tdm.IsDirty = false;
                    }
                }
                else
                {
                    MessageBox.Show("Undo failed.", "Git", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show("Failed to undo changes via git.", "Git", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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



        public IAsyncCommand SendChatCommand { get; init; }
        public ICommand UndoAIEditsCommand { get; init; }
        public ICommand NewChatCommand { get; init; }

        private void InitializeFileWatcher(string folderPath)
        {
            _fileWatcher?.Dispose();

            _fileWatcher = new FileSystemWatcher(folderPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Created += OnFileChanged;
            _fileWatcher.Deleted += OnFileChanged;
            _fileWatcher.Renamed += OnFileRenamed;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Only trigger for relevant file types
            if (e.Name != null && (e.Name.EndsWith(".puml", StringComparison.OrdinalIgnoreCase) ||
                                    e.Name.EndsWith(".class", StringComparison.OrdinalIgnoreCase) ||
                                    e.Name.EndsWith(".component", StringComparison.OrdinalIgnoreCase) ||
                                    e.Name.EndsWith(".seq", StringComparison.OrdinalIgnoreCase)))
            {
                _messageCheckerTrigger.Writer.TryWrite(true);
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (e.Name != null && (e.Name.EndsWith(".puml", StringComparison.OrdinalIgnoreCase) ||
                                    e.Name.EndsWith(".class", StringComparison.OrdinalIgnoreCase) ||
                                    e.Name.EndsWith(".component", StringComparison.OrdinalIgnoreCase) ||
                                    e.Name.EndsWith(".seq", StringComparison.OrdinalIgnoreCase)))
            {
                _messageCheckerTrigger.Writer.TryWrite(true);
            }
        }

        private void ApplyGitStatuses(IDictionary<string, GitFileStatus> map)
        {
            if (Folder == null) return;

            void Walk(TreeViewModel node)
            {
                if (map.TryGetValue(node.FullPath, out var status))
                {
                    node.GitStatus = status;
                }
                else
                {
                    // For folders or files with no entry, clear to Unmodified
                    node.GitStatus = GitFileStatus.Unmodified;
                }

                foreach (var child in node.Children)
                {
                    Walk(child);
                }
            }

            Walk(Folder);

            // Update current branch name opportunistically
            var folder = GetWorkingFolder();
            if (!string.IsNullOrEmpty(folder))
            {
                var git = new PlantUMLEditor.Models.Runners.GitSupport();
                CurrentGitBranch = git.GetCurrentBranch(folder);
            }
        }
    }
}