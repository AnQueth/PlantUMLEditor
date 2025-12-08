using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Prism.Commands;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
        // Document management command properties
        public DelegateCommand<BaseDocumentModel> CloseDocument { get; }
        public DelegateCommand<BaseDocumentModel> CloseDocumentAndSave { get; }
        public DelegateCommand<BaseDocumentModel> SaveCommand { get; }
        public DelegateCommand SaveAllCommand { get; }
        public DelegateCommand<BaseDocumentModel> SelectDocumentCommand { get; }

        private static async Task Save(TextDocumentModel doc)
        {
            await doc.Save();
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

            // Cleanup FileSystemWatcher
            _fileWatcher?.Dispose();
            _fileWatcher = null;

            CanClose = true;

            _ = Application.Current.Dispatcher.InvokeAsync(() => _window.Close());
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

        private async Task AfterCreate(TextDocumentModel d)
        {
            lock (_docLock)
            {
                OpenDocuments.Add(d);
            }

            await d.Save();

            CurrentDocument = d;
        }

        private void SelectDocumentHandler(BaseDocumentModel model)
        {
            CurrentDocument = model;
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
    }
}
