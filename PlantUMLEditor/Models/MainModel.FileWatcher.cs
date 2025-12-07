using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
        // Channel-based file system event pipeline (single reader)
        private readonly Channel<string> _fsEventChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        private CancellationTokenSource? _fsEventProcessingCts;
        private Task? _fsEventProcessingTask;
        private static readonly TimeSpan FsEventDebounce = TimeSpan.FromMilliseconds(500);

        private void InitializeFileWatcherChannel(string folderPath)
        {
            // Dispose previous watcher if present
            try
            {
                _fileWatcher?.Dispose();
            }
            catch { }

            // Cancel previous consumer if running
            try
            {
                _fsEventProcessingCts?.Cancel();
            }
            catch { }

            _fsEventProcessingCts?.Dispose();
            _fsEventProcessingCts = new CancellationTokenSource();

            _fileWatcher = new FileSystemWatcher(folderPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // Event handlers must be lightweight -> write path into channel
            _fileWatcher.Changed += (s, e) =>
            {
               
                _fsEventChannel.Writer.TryWrite(e.FullPath);
            };
            _fileWatcher.Created += (s, e) => _fsEventChannel.Writer.TryWrite(e.FullPath);
            _fileWatcher.Deleted += (s, e) => _fsEventChannel.Writer.TryWrite(e.FullPath);
            _fileWatcher.Renamed += (s, e) =>
            {
                // push both old and new so consumer can handle rename
                _fsEventChannel.Writer.TryWrite(e.OldFullPath);
                _fsEventChannel.Writer.TryWrite(e.FullPath);
            };

            // Start the single-reader background consumer
            _fsEventProcessingTask = Task.Run(() => ProcessFsEventsAsync(_fsEventProcessingCts!.Token));
        }

        private async Task ProcessFsEventsAsync(CancellationToken ct)
        {
            var reader = _fsEventChannel.Reader;
            var batch = new List<string>(capacity: 128);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Wait until at least one item is available
                    if (!await reader.WaitToReadAsync(ct).ConfigureAwait(false))
                        break;

                    // Drain immediate items
                    while (reader.TryRead(out var item))
                    {
                        if (!string.IsNullOrEmpty(item))
                            batch.Add(item);
                    }

                    // Debounce window to collect bursty events
                    try
                    {
                        await Task.Delay(FsEventDebounce, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        // proceed to draining and exit soon
                    }

                    // Drain any that arrived during debounce
                    while (reader.TryRead(out var item))
                    {
                        if (!string.IsNullOrEmpty(item))
                            batch.Add(item);
                    }

                    if (batch.Count == 0)
                        continue;

                    // Deduplicate and freeze the set for processing
                    var paths = batch.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    batch.Clear();

                    // Marshal UI updates to WPF dispatcher
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() => UpdateTreeForPaths(paths));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                    lock (_docLock)
                    {
                        foreach (var p in paths)
                        {
                            var doc = OpenDocuments.FirstOrDefault(d => string.Equals(d.FileName, p, StringComparison.Ordinal));
                            if (doc is TextDocumentModel tdm)
                            {
                               
                              
                                    tdm.Content = File.ReadAllText(p);
                              
                               
                            }
                        }
                    }
                    // Preserve existing behavior: trigger message checker for puml changes
                    if (paths.Any(p => p.EndsWith(FileExtension.PUML.Extension, StringComparison.OrdinalIgnoreCase)))
                    {
                        _messageCheckerTrigger.Writer.TryWrite(true);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void UpdateTreeForPaths(IList<string> paths)
        {
            if (Folder == null || string.IsNullOrEmpty(FolderBase))
                return;

            // Find the node representing the workspace root (ScanDirectory creates this)
            TreeViewModel? workspaceRoot = FindNodeByPath(Folder, FolderBase);
            if (workspaceRoot == null && Folder.Children.Count > 0)
            {
                workspaceRoot = Folder.Children[0];
            }

            if (workspaceRoot == null)
                return;

            foreach (string rawPath in paths)
            {
                try
                {
                    if (string.IsNullOrEmpty(rawPath))
                        continue;

                    string path = rawPath;

                    // ignore temporary save files
                    if (path.EndsWith(TemporarySave.Extension, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // ignore files that start with '.' (e.g. .gitignore)
                    string fileName = Path.GetFileName(path);
                    if (!string.IsNullOrEmpty(fileName) && fileName.StartsWith(".", StringComparison.InvariantCulture))
                        continue;

                    // ignore anything inside dot-prefixed folders
                    if (IsUnderDotFolder(path))
                        continue;

                    if (Directory.Exists(path))
                    {
                        EnsureFolderNodes(workspaceRoot, path);
                        continue;
                    }

                    if (File.Exists(path))
                    {
                        string parentDir = Path.GetDirectoryName(path) ?? string.Empty;
                        // skip if parent folder is dot-prefixed
                        if (IsUnderDotFolder(parentDir))
                            continue;

                        var parentNode = EnsureFolderNodes(workspaceRoot, parentDir);
                        if (parentNode == null)
                            continue;

                        var existing = parentNode.Children.FirstOrDefault(c => c.IsFile && string.Equals(c.FullPath, path, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            existing.FullPath = path;
                            existing.Name = Path.GetFileName(path);
                            existing.IsUML = string.Equals(Path.GetExtension(path), FileExtension.PUML.Extension, StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            parentNode.Children.Insert(0, new TreeViewModel(parentNode, path, Statics.GetIcon(path)));
                        }

                        continue;
                    }

                    // Not present on disk -> remove node if found (deleted or renamed away)
                    var node = FindNodeByPath(Folder, path);
                    if (node != null && node.Parent != null)
                    {
                        node.Parent.Children.Remove(node);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            // Update command state after structural changes
            ScanAllFiles.RaiseCanExecuteChanged();
            SaveAllCommand.RaiseCanExecuteChanged();
            CreateNewClassDiagram.RaiseCanExecuteChanged();
            CreateNewComponentDiagram.RaiseCanExecuteChanged();
            CreateNewSequenceDiagram.RaiseCanExecuteChanged();
            GitCommitAndSyncCommand.RaiseCanExecuteChanged();
        }

        private bool IsUnderDotFolder(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(FolderBase))
                return false;

            string rootFull = Path.GetFullPath(FolderBase);
            string candidate;
            try
            {
                candidate = Path.GetFullPath(fullPath);
            }
            catch
            {
                return false;
            }

            if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return false;

            string relative = Path.GetRelativePath(rootFull, candidate);
            if (string.IsNullOrEmpty(relative) || relative == ".")
                return false;

            string[] parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Length > 0 && part.StartsWith(".", StringComparison.InvariantCulture))
                    return true;
            }

            return false;
        }

        private TreeViewModel? FindNodeByPath(TreeViewModel root, string fullPath)
        {
            if (string.Equals(root.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                return root;

            foreach (var c in root.Children)
            {
                var found = FindNodeByPath(c, fullPath);
                if (found != null) return found;
            }

            return null;
        }

        private TreeViewModel? EnsureFolderNodes(TreeViewModel workspaceRoot, string folderPath)
        {
            if (string.IsNullOrEmpty(FolderBase))
                return null;

            string rootFull = Path.GetFullPath(FolderBase);
            string candidate;
            try
            {
                candidate = Path.GetFullPath(folderPath);
            }
            catch
            {
                return null;
            }

            if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return null;

            string relative = Path.GetRelativePath(rootFull, candidate);
            if (string.IsNullOrEmpty(relative) || relative == ".")
                return workspaceRoot;

            string[] parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            // Respect skipping dot-folders: if any part starts with '.' do not create nodes under them
            foreach (var part in parts)
            {
                if (part.Length > 0 && part.StartsWith(".", StringComparison.InvariantCulture))
                    return null;
            }

            TreeViewModel current = workspaceRoot;
            string accum = rootFull;
            foreach (string part in parts)
            {
                accum = Path.Combine(accum, part);
                var child = current.Children.FirstOrDefault(c => !c.IsFile && string.Equals(c.FullPath, accum, StringComparison.OrdinalIgnoreCase));
                if (child == null)
                {
                    var folderNode = new FolderTreeViewModel(current, accum, true, Statics.GetClosedFolderIcon());
                    current.Children.Add(folderNode);
                    child = folderNode;
                }
                current = child;
            }

            return current;
        }
    }
}