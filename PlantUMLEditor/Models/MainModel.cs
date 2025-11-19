using Markdig;
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
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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


        private readonly AutoResetEvent _messageCheckerTrigger = new(true);
        private readonly MainWindow _window;

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

        private TreeViewModel? _selectedFile;
        private GlobalFindResult? _selectedFindResult;

        private TreeViewModel? _selectedFolder;
        private string? _selectedMRUFolder;

        private bool _spellCheck;
        private BaseDocumentModel? _currentDocument;


        private readonly NewFileManager _newFileManager;
        private readonly TemplateStorage _templateStorage;
        private DocumentMessage? selectedMessage;

        public TemplateStorage TemplateStorage
        {
            get => _templateStorage;
        }
        public MainModel(IIOService openDirectoryService,
            IUMLDocumentCollectionSerialization documentCollectionSerialization,
            MainWindow mainWindow)
        {
            _window = mainWindow;
            CancelExecutingAction = new DelegateCommand(CancelCurrentExecutingAction, () =>
            {
                return _cancelCurrentExecutingAction != null;
            });

            GotoDefinitionCommand = new DelegateCommand<string>(GotoDefinitionInvoked);
            FindAllReferencesCommand = new DelegateCommand<string>(FindAllReferencesInvoked);
            Documents = new UMLModels.UMLDocumentCollection();
            SendChatCommand = new AsyncDelegateCommand(SendChat, () => ChatText.Length > 0);
            _ioService = openDirectoryService;
            OpenDirectoryCommand = new DelegateCommand(() => _ = OpenDirectoryHandler());
            DeleteMRUCommand = new DelegateCommand<string>((s) => DeleteMRUCommandHandler(s));
            OpenHelpCommand = new DelegateCommand(OpenHelpCommandHandler);
            SaveAllCommand = new DelegateCommand(SaveAllHandler, () => !string.IsNullOrEmpty(FolderBase));
            Folder = new FolderTreeViewModel(null, Path.GetTempPath(), true, Statics.GetClosedFolderIcon());
            _documentCollectionSerialization = documentCollectionSerialization;
            OpenDocuments = new ObservableCollection<BaseDocumentModel>();
            CreateNewJSONDocumentCommand = new DelegateCommand(NewJsonDiagramHandler, () => !string.IsNullOrEmpty(FolderBase));
            CreateNewUnknownDiagram = new DelegateCommand(NewUnknownDiagramHandler, () => !string.IsNullOrEmpty(FolderBase));
            OpenUMLColorConfigCommand = new DelegateCommand(OpenUMLColorConfigHandler);
            OpenMDColorConfigCommand = new DelegateCommand(OpenMDColorConfigHandler);
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
            _ = _templateStorage.Load(AppSettings.Default.TemplatePath);

            EditorFontSize = AppSettings.Default.EditorFontSize;
        }


        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SelectedMessage = null;
        }

        private int _selectedTab;
        public int SelectedToolTab
        {
            get => _selectedTab;
            set => SetValue(ref _selectedTab, value);
        }
        private void ApplyTemplateCommandHandler()
        {
            if (CurrentDocument is TextDocumentModel tdm)
            {
                TemplateProcessorModel tpm = new TemplateProcessorModel(SelectedTemplate);
                TemplateProcessorWindow tpw = new(tpm);
                if (tpw.ShowDialog().GetValueOrDefault())
                {
                    tdm.InsertAtCursor(tpm.ProcessedContent);

                }


            }
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

        private bool _templatesEnabled = false;
        public bool TemplatesEnabled
        {
            get => _templatesEnabled;
            set => SetValue(ref _templatesEnabled, value);
        }

        private TemplateItem? _selectedTemplate;


        public TemplateItem? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                SetValue(ref _selectedTemplate, value);
                ApplyTemplateCommand.RaiseCanExecuteChanged();


            }
        }

        private void EditTemplatesCommandHandler()
        {
            var win = new TemplateEditorWindow();
            TemplatesViewModel vm = new(_templateStorage);
            win.DataContext = vm;
            win.ShowDialog();

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

        private void OpenExplorerHandler()
        {
            ExplorerRunner.Run(FolderBase);
        }

        private void OpenTerminalHandler()
        {
            TerminalRunner.Run(FolderBase);
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

        public DelegateCommand CreateNewUnknownDiagram
        {
            get;
        }
        public DelegateCommand OpenUMLColorConfigCommand { get; }
        public DelegateCommand OpenMDColorConfigCommand { get; }
        public DelegateCommand OpenUMLColorCodingCommand { get; }
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
                _messageCheckerTrigger.Set();
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
                if (!string.IsNullOrEmpty(_selectedMRUFolder) && value != FolderBase)
                {
                    _ = OpenDirectoryHandler(false, _selectedMRUFolder);
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
            if (CurrentDocument is not null)
            {
                if (string.Equals(Path.GetExtension(CurrentDocument.FileName), ".md", StringComparison.OrdinalIgnoreCase))
                {
                    e.Effects = DragDropEffects.Link;
                    e.Handled = true;
                }
            }
            e.Handled = true;
        }

        public void TextDrop(object sender, DragEventArgs e)
        {
            if (CurrentDocument is not null)
            {
                if (string.Equals(Path.GetExtension(CurrentDocument.FileName), ".md", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = (string)e.Data.GetData(DataFormats.StringFormat);
                    if (CurrentDocument is TextDocumentModel tdm)
                    {
                        string name = Path.GetFileNameWithoutExtension(fileName);
                        fileName = Path.GetRelativePath(Path.GetDirectoryName(CurrentDocument.FileName), fileName);
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
                            await ScanDirectory(this.FolderBase);
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
                if (model is not null and not FolderTreeViewModel)
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
                    DocFXServeCommand.RaiseCanExecuteChanged();
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
                    if (tvm.IsFile && !tvm.IsRenaming)
                    {
                        DragDrop.DoDragDrop(fe, tvm.FullPath, DragDropEffects.Link | DragDropEffects.Move);
                    }
                }
            }
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

        private async Task AddDefaultDataType(TextDocumentModel[] textDocumentModels, MissingDataTypeMessage missingDataTypeMessage)
        {
            UMLClassDiagram? f = Documents.ClassDocuments.FirstOrDefault(p => string.CompareOrdinal(p.Title, DEFAULTSCLASS) == 0);
            if (f != null)
            {
                f.Package.Children.Add(new UMLClass("", "default", null, false, missingDataTypeMessage.MissingDataTypeName, new List<UMLDataType>()));

                ClassDiagramDocumentModel? od = textDocumentModels.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => p.FileName == f.FileName);
                if (od != null)
                {
                    CurrentDocument = od;
                    od.UpdateDiagram(f);
                }
                else
                {
                    od = await OpenDocumenntManager.OpenClassDiagram(f.FileName, f, 0, null) as ClassDiagramDocumentModel;

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
                model.Children.Add(new TreeViewModel(model, file, Statics.GetIcon(file)));
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

                FolderTreeViewModel? fm = new FolderTreeViewModel(model, item, isExpanded, Statics.GetClosedFolderIcon());
                model.Children.Add(fm);

                await AddFolderItems(item, fm);
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
                    od = await OpenDocumenntManager.OpenClassDiagram(doc.FileName, doc, 0, null) as ClassDiagramDocumentModel;
                    if (od is not null)
                    {
                        CurrentDocument = od;
                        od.UpdateDiagram(doc);
                    }
                }
            }
        }


        private async Task AttemptOpeningFile(string fullPath,
            int lineNumber = 0, string? searchText = null)
        {

            CurrentDocument = await OpenDocumenntManager.TryOpen(fullPath, lineNumber, searchText);

            lock (_docLock)
            {
                if (CurrentDocument is not null && !OpenDocuments.Contains(CurrentDocument))
                {
                    OpenDocuments.Add(CurrentDocument);
                }

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

                await _closingBlocker.Wait();


                if (!AllowContinueClosing)
                {
                    SelectedMRUFolder = FolderBase;
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
                    if (string.IsNullOrEmpty(_metaDataFile) || string.IsNullOrEmpty(FolderBase))
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
                    List<DocumentMessage>? newMessages = documentMessageGenerator.Generate(FolderBase);

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

            PlantUMLImageGenerator generator = new PlantUMLImageGenerator(AppSettings.Default.JARLocation,
                _selectedFile.FullPath, dir);

            TreeViewModel? folder = FindFolderContaining(Folder, _selectedFile.FullPath);

            PlantUMLImageGenerator.UMLImageCreateRecord? res = await generator.Create();

            if (folder != null)
            {
                TreeViewModel? file = folder.Children.First(z => string.Equals(z.FullPath, _selectedFile.FullPath, StringComparison.Ordinal));

                int ix = folder.Children.IndexOf(file);
                if (!folder.Children.Any(z => z.FullPath == res.fileName))
                {
                    folder.Children.Insert(ix, new TreeViewModel(folder, res.fileName, Statics.GetIcon(res.fileName)));
                }
            }
        }

        private void DeleteMRUCommandHandler(string mru)
        {
            MRUFolders.Remove(mru);
            SaveMRU();
        }

        private void DocFXServeCommandHandler()
        {
            DOCFXRunner.Run(FolderBase);


        }

        private void FindAllReferencesInvoked(string text)
        {
            SelectedToolTab = 2;
            FindReferenceResults.Clear();

            foreach (var item in DataTypeServices.FindAllReferences(Documents, text))
            {
                FindReferenceResults.Add(item);
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
                FolderBase = AppSettings.Default.WorkingDir;
            }

            if (string.IsNullOrEmpty(FolderBase))
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

                FolderBase = dir;
            }

            if (FolderBase == null || !Directory.Exists(FolderBase))
            {
                return null;
            }

            _metaDataDirectory = Path.Combine(FolderBase, ".umlmetadata");

            if (!Directory.Exists(_metaDataDirectory))
            {
                Directory.CreateDirectory(_metaDataDirectory);
            }

            _metaDataFile = Path.Combine(_metaDataDirectory, DATAJSON);
            return FolderBase;
        }

        private async void GitCommitAndSyncCommandHandler()
        {

            GitMessages = null;
            GitSupport gs = new GitSupport();
            if (string.IsNullOrEmpty(FolderBase))
            {
                return;
            }

            SelectedToolTab = 3;
            (var reload, GitMessages) = await gs.CommitAndSync(FolderBase);
            if (reload)
            {
                await ScanAllFilesHandler();
            }
        }

        private async void GlobalSearchHandler(string obj)
        {
            if (string.IsNullOrEmpty(FolderBase))
            {
                return;
            }



            List<GlobalFindResult>? findresults = await GlobalSearch.Find(obj, new string[]
            {WILDCARD + FileExtension.PUML.Extension, WILDCARD + FileExtension.MD.Extension, WILDCARD + FileExtension.YML.Extension
            });
            GlobalFindResults.Clear();
            foreach (GlobalFindResult? f in findresults)
            {
                GlobalFindResults.Add(f);
            }
        }

        private async void GotoDefinitionInvoked(string text)
        {


            foreach (var item in DataTypeServices.GotoDefinition(Documents, text))
            {
                await AttemptOpeningFile(item.FileName, item.DataType.LineNumber, null);
            }
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
        private async Task AfterCreate(TextDocumentModel d)
        {
            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }

            await d.Save();

            CurrentDocument = d;
        }


        private async void NewClassDiagramHandler()
        {
            await _newFileManager.CreateNewClassDiagram(_selectedFolder, FolderBase);

        }



        private async void NewComponentDiagramHandler()
        {
            await _newFileManager.CreateNewComponentDiagram(_selectedFolder, FolderBase);

        }

        private async void NewJsonDiagramHandler()
        {
            await _newFileManager.CreateNewJsonDiagram(_selectedFolder, FolderBase);


        }





        private async void NewMarkDownDocumentHandler()
        {
            await _newFileManager.CreateNewMarkdownFile(_selectedFolder, FolderBase);

        }



        private async void NewSequenceDiagramHandler()
        {
            await _newFileManager.CreateNewSequenceFile(_selectedFolder, FolderBase);

        }

        private async void NewUnknownDiagramHandler()
        {
            await _newFileManager.CreateNewUnknownDiagramFile(_selectedFolder, FolderBase);

        }





        private async void NewYAMLDocumentHandler()
        {
            await _newFileManager.CreateNewYamlFile(_selectedFolder, FolderBase);

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

                string? oldFolder = FolderBase;

                FolderBase = null;
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
                    FolderBase = oldFolder;
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

        private void SaveMRU()
        {
            AppSettings.Default.MRU = JsonConvert.SerializeObject(MRUFolders);

            AppSettings.Default.Save();
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

            FolderTreeViewModel? start = new FolderTreeViewModel(Folder, dir, true, Statics.GetClosedFolderIcon());

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


        public class ChatMessage(bool isUser) : INotifyPropertyChanged
        {

            private bool _isBusy = true;
            public bool IsBusy
            {
                get
                {
                    return _isBusy;
                }
                set
                {
                    _isBusy = value;
                    OnPropertyChanged(nameof(IsBusy));
                }
            }

            public class ToolCall
            {
                // allow nullable since some tool result objects may not have all fields
                public string? Id { get; set; }
                public string? ToolName { get; set; }
                public string? Arguments { get; set; }
                public string? Result { get; set; }

            }

            public bool IsUser { get; } = isUser;

            public ObservableCollection<ToolCall> ToolCalls { get; } = new ObservableCollection<ToolCall>();

            public FlowDocument Document
            {
                get
                {
                    MarkdownPipeline? pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

                    var doc =Markdig.Wpf.Markdown.ToFlowDocument(Message, pipeline);

                    CompactFlowDocument(doc);

                    return doc;

                }
            }


            private string _message = string.Empty;
            public string Message
            {
                get => _message;
                set
                {
                    _message = value;
                    OnPropertyChanged(nameof(Message));
                    OnPropertyChanged(nameof(Document));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged(string name)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
        public ObservableCollection<ChatMessage> AIConversation { get; } = new ObservableCollection<ChatMessage>();

        private string _chatText = string.Empty;

        public string ChatText
        {
            get
            {
                return _chatText;
            }
            set
            {
                _chatText = value;
                (SendChatCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();

                PropertyChangedInvoke(nameof(ChatText));

            }
        }


        public IAsyncCommand SendChatCommand { get; init; }

        private ChatClientAgentThread _convThread = null;

        [Description("Replaces all occurrences of 'text' with 'newText' in the document.")]
        public void ReplaceText([Description("the text to find")] string text, [Description("the new text")] string newText)
        {
            if (_currentTdm != null)
            {
                var t = _currentTdm.Content;
                t = t.Replace(text, newText);
                _currentTdm.Content = t;
            }
        }

        [Description("Inserts the specified text at the given position.")]
        public void InsertTextAtPosition([Description("position in the original text to insert at")] int position, [Description("the text to insert")] string text)
        {
            if (_currentTdm != null)
            {

                _currentTdm.InsertTextAt(text, position, text.Length);
            }
        }

        [Description("rewrite the complete document")]
        public void RewriteDocument([Description("the new text for the document")] string text)
        {
            if (_currentTdm != null)
            {
                _currentTdm.Content = text;
            }

        }

        [Description("reads the current text in the document.")]
        public string ReadDocumentText()
        {
            if (_currentTdm != null)
            {
                return _currentTdm.Content;
            }

            return string.Empty;
        }

        [Description("search for a term in all documents")]
        public async Task<List<GlobalFindResult>> SearchInDocuments([Description("the text to search for. it can be a word or regex.")] string text)
        {

            string WILDCARD = "*";
            List<GlobalFindResult>? findresults = await GlobalSearch.Find(text, new string[]
            {WILDCARD + FileExtension.PUML.Extension, WILDCARD + FileExtension.MD.Extension, WILDCARD + FileExtension.YML.Extension
            });

            return findresults;

        }

        [Description("read a file by a path")]
        public async Task<string> ReadFileByPath([Description("the full path to the file")] string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is null or empty", nameof(path));

            if (string.IsNullOrEmpty(FolderBase) || !Directory.Exists(FolderBase))
                throw new InvalidOperationException("Root directory is not set or does not exist.");

            string root = System.IO.Path.GetFullPath(FolderBase);
            if (!root.EndsWith(System.IO.Path.DirectorySeparatorChar))
            {
                root += System.IO.Path.DirectorySeparatorChar;
            }

            string fullPath;
            if (System.IO.Path.IsPathRooted(path))
            {
                fullPath = System.IO.Path.GetFullPath(path);
            }
            else
            {
                // resolve relative path against the root directory
                fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, path));
            }

            // Normalize for comparison
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Access to files outside the workspace root is not allowed.");
            }

            return await File.ReadAllTextAsync(fullPath);
        }


        public ICommand NewChatCommand { get; init; }
        private TextDocumentModel _currentTdm;

         private void NewChatCommandHandler()
        {
      
            AIConversation.Clear();
            _convThread = null;
        }

        private async Task SendChat()
        {
            AIAgentFactory factory = new AIAgentFactory();
            var agent = factory.Create(new AISettings()
            {
                Deployment = AppSettings.Default.AzureAIDeployment,
                Endpoint = AppSettings.Default.AzureAIEndpoint,
                Key = AppSettings.Default.AzureAIKey,
                SourceName = "PlantUML"
            }, new Delegate[] {
                ReplaceText,
                InsertTextAtPosition,
                RewriteDocument,
                SearchInDocuments,
                ReadDocumentText
            });

            if (_convThread == null)
            {
                _convThread = (ChatClientAgentThread)agent.GetNewThread();
            }
            AIConversation.Add(new ChatMessage(true)
            {
                Message = ChatText,
                IsBusy = false

            });
            ChatMessage cm = new ChatMessage(false);

            AIConversation.Add(cm);

            var prompt = $"User input: \n{ChatText}";


            ChatText = string.Empty;

            _currentTdm = CurrentDocument as TextDocumentModel;
            if (_currentTdm is null)
            {
                cm.Message = "No text document is currently open to interact with the AI.";
                cm.IsBusy = false;
                return;
            }


            try
            {
                await foreach (var item in agent.RunStreamingAsync(prompt, _convThread))
                {
                    foreach (var c in item.Contents)
                    {
                        if (c is FunctionCallContent fc)
                        {
                            cm.ToolCalls.Add(new ChatMessage.ToolCall
                            {
                                ToolName = fc.Name,
                                Arguments = System.Text.Json.JsonSerializer.Serialize(fc.Arguments)
                            });
                        }
                    }

                    cm.Message += item;
                }

            }
            catch (Exception ex)
            {
                cm.Message += $"\n\nError: {ex.Message}";
            }

            cm.IsBusy = false;


           





        }

        static void CompactFlowDocument(System.Windows.Documents.FlowDocument doc)
        {
            doc.PagePadding = new System.Windows.Thickness(6);
            doc.ColumnWidth = double.PositiveInfinity; // avoid column wrapping
            foreach (var block in doc.Blocks.ToList())
                CompactBlock(block);
        }

        static void CompactBlock(System.Windows.Documents.Block block)
        {
            switch (block)
            {
                case System.Windows.Documents.Paragraph p:
                    p.Margin = new System.Windows.Thickness(0, 0, 0, 4);
                    p.LineStackingStrategy = System.Windows.LineStackingStrategy.MaxHeight;
                    p.LineHeight = p.FontSize * 1.15;
                    break;

                case System.Windows.Documents.List list:
                    list.Margin = new System.Windows.Thickness(0, 0, 0, 4);
                    list.MarkerOffset = 12;
                    foreach (var li in list.ListItems)
                    {
                        li.Margin = new System.Windows.Thickness(0, 0, 0, 2);
                        foreach (var inner in li.Blocks.ToList()) CompactBlock(inner);
                    }
                    break;

                case System.Windows.Documents.Section s:
                    s.Margin = new System.Windows.Thickness(0);
                    foreach (var inner in s.Blocks.ToList()) CompactBlock(inner);
                    break;

                default:
                    // handle other block types if needed
                    break;
            }
        }
    }
}