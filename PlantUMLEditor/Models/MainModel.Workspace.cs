using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using PlantUMLEditor.Models.Runners;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
            private string? _selectedMRUFolder;

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

    
        private void OpenExplorerHandler()
        {
            ExplorerRunner.Run(FolderBase);
        }

        private void OpenTerminalHandler()
        {
            TerminalRunner.Run(FolderBase);
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

        private void SaveMRU()
        {
            AppSettings.Default.MRU = JsonConvert.SerializeObject(MRUFolders);

            AppSettings.Default.Save();
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


                StartGitStatusMonitor();

            }
            finally
            {
                _checkMessagesRunning.Release();
            }

            // Initialize FileSystemWatcher for the new workspace
            if (!string.IsNullOrEmpty(FolderBase))
            {
                InitializeFileWatcher(FolderBase);
            }

            _messageCheckerTrigger.Writer.TryWrite(true);
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

        private async Task AddFolderItems(string dir, TreeViewModel model)
        {
            if ((_cancelCurrentExecutingAction?.IsCancellationRequested).GetValueOrDefault())
            {
                return;
            }

            CurrentActionExecuting = $"Reading {dir}";

            await Task.Delay(1); //sleep for ui updates

            foreach (string file in Directory.EnumerateFiles(dir))
            {
                if (Path.GetFileName(file).EndsWith(TemporarySave.Extension, StringComparison.Ordinal))
                    continue;
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
    }
}
