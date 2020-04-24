using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
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

        public TreeViewModel Folder
        {
            get { return folder; }
            private set { SetValue(ref folder, value); }
        }

        public ObservableCollection<DocumentModel> OpenDocuments
        {
            get;
        }

        public ObservableCollection<DocumentMessage> Messages
        {
            get;
        }

        public DelegateCommand CreateNewSequenceDiagram { get; }
        public DelegateCommand CreateNewClassDiagram { get; }
        public DelegateCommand<DocumentModel> CloseDocument { get; }
        public DelegateCommand<DocumentModel> CloseDocumentAndSave { get; }

        private readonly IUMLDocumentCollectionSerialization _documentCollectionSerialization;

        public DelegateCommand ScanAllFiles { get; }

        private Timer _messageChecker;


        public DocumentMessage SelectedMessage
        {
            get { return selectedMessage; }
            set
            {
                SetValue(ref selectedMessage, value);

                if (value != null)
                    AttemptOpeningFile(selectedMessage.FileName);

            }
        }

        private void CheckMessages(object state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {

                List<DocumentMessage> newMessages = new List<DocumentMessage>();


                var items = from o in Documents.SequenceDiagrams
                            let w = from z in o.LifeLines
                                    where z.Warning != null
                                    select z
                            from f in w
                            where f.Warning != null
                            select new { o, f };






                foreach (var i in items)
                {


                    newMessages.Add(new DocumentMessage()
                    {
                        FileName = i.o.FileName,
                        Text = i.f.Warning,
                        LineNumber = i.f.LineNumber,
                        Warning = true
                    });


                }



                foreach (var i in Documents.SequenceDiagrams)
                {
                    CheckEntities(i.FileName, i.Entities);

                }


                List<DocumentMessage> removals = new List<DocumentMessage>();
                foreach (var item in Messages)
                {
                    DocumentMessage m;
                    if ((m = newMessages.FirstOrDefault(z => z.FileName == item.FileName && z.Text == item.Text)) == null)
                    {
                        removals.Add(item);

                    }
                }

                removals.ForEach(p => Messages.Remove(p));

                foreach (var item in newMessages)
                {
                    DocumentMessage m;
                    if ((m = Messages.FirstOrDefault(z => z.FileName == item.FileName && z.Text == item.Text)) == null)
                    {
                        Messages.Add(item);
                    }
                }




                _messageChecker.Change(2000, Timeout.Infinite);


                void CheckEntities(string fileName, List<UMLOrderedEntity> entities)
                {
                    foreach (var g in entities)
                    {
                        if (g.Warning != null)
                        {
                            newMessages.Add(new DocumentMessage()
                            {
                                FileName = fileName,
                                LineNumber = g.LineNumber,
                                Text = g.Warning,
                                Warning = true
                            });
                        }

                        if (g is UMLSequenceBlockSection s)
                        {
                            CheckEntities(fileName, s.Entities);

                        }
                    }
                }

            });
        }

        public MainModel(IOpenDirectoryService openDirectoryService, IUMLDocumentCollectionSerialization documentCollectionSerialization)
        {
            Documents = new UMLModels.UMLDocumentCollection();
            _messageChecker = new Timer(CheckMessages, null, 1000, Timeout.Infinite);
            _openDirectoryService = openDirectoryService;
            OpenDirectoryCommand = new DelegateCommand(OpenDirectory);
            SaveAllCommand = new DelegateCommand(() => SaveAll());
            Folder = new TreeViewModel(Path.GetTempPath(), false);
            _documentCollectionSerialization = documentCollectionSerialization;
            OpenDocuments = new ObservableCollection<DocumentModel>();
            CreateNewSequenceDiagram = new DelegateCommand(NewSequenceDiagramHandler);
            CreateNewClassDiagram = new DelegateCommand(NewClassDiagramHandler);
            CloseDocument = new DelegateCommand<DocumentModel>(CloseDocumentHandler);
            CloseDocumentAndSave = new DelegateCommand<DocumentModel>(CloseDocumentAndSaveHandler);
            Messages = new ObservableCollection<DocumentMessage>();
            ScanAllFiles = new DelegateCommand(ScanAllFilesHandler);



        }

        private async void ScanAllFilesHandler()
        {
            var folder = GetWorkingFolder();
            List<string> potentialSequenceDiagrams = new List<string>();
            await ScanForFiles(folder, potentialSequenceDiagrams);

            foreach (var seq in potentialSequenceDiagrams)
                await TryCreateSequenceDiagram(seq);

        }

        private async Task ScanForFiles(string folder, List<string> potentialSequenceDiagrams )
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.puml"))
            {
                if(null == await TryCreateClassDiagram(file))
                {
                    potentialSequenceDiagrams.Add(file);
                }

                 

            }
            foreach (var file in Directory.EnumerateDirectories(folder))
            {
                await ScanForFiles(file, potentialSequenceDiagrams);

            }
        }

        private async void CloseDocumentAndSaveHandler(DocumentModel doc)
        {
            await Save(doc);

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

        public async void TreeItemClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                return;

            TreeViewModel fbm = GetSelectedFile(Folder);
            if (fbm == null)
                return;

            await AttemptOpeningFile(fbm.FullPath);


        }

        private async Task AttemptOpeningFile(string fullPath)
        {
            var doc = OpenDocuments.FirstOrDefault(p => p.FileName == fullPath);

            if (doc != null)
            {
                CurrentDocument = doc;

                return;
            }

            var (cd, sd) = await TryFindOrAddDocument(fullPath);

            if (cd != null)
            {
                OpenClassDiagram(fullPath, cd);
            }
            else if (sd != null)
            {
                OpenSequenceDiagram(fullPath, sd);
            }
        }

        private async Task<(UMLClassDiagram cd, UMLSequenceDiagram sd)> TryFindOrAddDocument(string fullPath)
        {
            UMLClassDiagram cd = null;
            UMLSequenceDiagram sd = await TryCreateSequenceDiagram(fullPath);
            if (sd == null)
                cd = await TryCreateClassDiagram(fullPath);

            return (cd, sd);
        }

        private async Task<UMLSequenceDiagram> TryCreateSequenceDiagram(string fullPath)
        {
            var sd = Documents.SequenceDiagrams.Find(p => p.FileName == fullPath);
            if (sd == null)
            {
                try
                {
                    sd = await PlantUML.UMLSequenceDiagramParser.ReadFile(fullPath, Documents.DataTypes, false);
                }
                catch { }
                if (sd != null)
                {
                    Documents.SequenceDiagrams.RemoveAll(p => p.FileName == fullPath);
                    Documents.SequenceDiagrams.Add(sd);

                }
            }

            return sd;
        }

        private async Task<UMLClassDiagram> TryCreateClassDiagram(string fullPath)
        {
            var cd = Documents.ClassDocuments.Find(p => p.FileName == fullPath);
            if (cd == null)
            {


                try
                {
                    cd = await PlantUML.UMLClassDiagramParser.ReadFile(fullPath);
                }
                catch
                {

                }
                if (cd != null)
                {
                    Documents.ClassDocuments.RemoveAll(p => p.FileName == fullPath);
                    Documents.ClassDocuments.Add(cd);
                }
            }

            return cd;
        }

        private string GetNewFile()
        {
            TreeViewModel selected = GetSelectedFolder(Folder);

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
            if (old != null && string.IsNullOrEmpty(@new.FileName))
            {
                @new.FileName = old.FileName;
            }

            list.RemoveAll(z => z.FileName == @new.FileName || z.Title == @new.Title);
            list.Add(@new);
        }

        private void NewClassDiagram(string fileName, string title)
        {
            var s = new UMLModels.UMLClassDiagram(title, fileName);

            var d = new ClassDiagramDocumentModel((old, @new) => DiagramModelChanged(Documents.ClassDocuments, old, @new))
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

            Folder = new TreeViewModel("", false);

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

        private void AddFolderItems(string dir, TreeViewModel model)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.puml"))
            {
                model.Children.Add(new TreeViewModel(file, true)
                {
                    Name = Path.GetFileName(file)
                });
            }

            foreach (var item in Directory.EnumerateDirectories(dir))
            {
                if (Path.GetFileName(item).StartsWith("."))
                    continue;

                var fm = new TreeViewModel(item, false)
                {
                    Name = Path.GetFileName(item)
                };
                model.Children.Add(fm);

                AddFolderItems(item, fm);
            }
        }

        private readonly IOpenDirectoryService _openDirectoryService;
        private TreeViewModel folder;
        private UMLModels.UMLDocumentCollection documents;
        private DocumentModel currentDocument;
        private string _folderBase;
        private DocumentMessage selectedMessage;

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