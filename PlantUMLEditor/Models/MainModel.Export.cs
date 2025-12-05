using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using PlantUMLEditor.Models.Runners;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
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

        private void DocFXServeCommandHandler()
        {
            DOCFXRunner.Run(FolderBase);
        }

        private (List<string> files, string statusText) GetStatus(Dictionary<string, GitFileStatus> existingStatus)
        {
            List<string> files = new List<string>();
            var sb = new StringBuilder();

            foreach (var item in existingStatus)
            {


                var statusStr = item.Value switch
                {
                    GitFileStatus.Untracked => "[New]",
                    GitFileStatus.Modified => "[Modified]",
                    GitFileStatus.Deleted => "[Deleted]",

                    _ => $"[{item.Value.ToString()}]"
                };


                files.Add($"{statusStr} {item.Key}");
            }

            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Total changed files: {files.Count}");

            if (files.Count == 0)
            {
                sb.AppendLine("Working directory is clean.");
            }


            return (files, sb.ToString());
        }

        private async void GitCommitAndSyncCommandHandler()
        {
            if (string.IsNullOrEmpty(FolderBase))
            {
                return;
            }

            GitMessages = null;
            GitSupport gs = new GitSupport();

            // Get changed files first

            var rawStatus = gs.GetRawStatus(FolderBase);
            var (changedFiles, statusText) = GetStatus(rawStatus);

            if (changedFiles.Count == 0)
            {
                GitMessages = statusText;
                SelectedToolTab = 3;
                return;
            }

            // Show commit message dialog
            var viewModel = new CommitMessageViewModel(changedFiles);
            var dialog = new CommitMessageWindow
            {
                DataContext = viewModel,
                Owner = _window
            };

            if (dialog.ShowDialog() != true)
            {
                GitMessages = "Commit cancelled.";
                SelectedToolTab = 3;
                return;
            }

            // Proceed with commit using custom message
            CurrentActionExecuting = "Committing and syncing...";
            SelectedToolTab = 3;

            (var hadConflict, GitMessages) = await gs.CommitAndSync(FolderBase, viewModel.CommitMessage);

            CurrentActionExecuting = null;

            if (hadConflict)
            {
                MessageBox.Show(_window, "Git conflict detected. Please resolve manually.",
                               "Git Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
                await ScanAllFilesHandler();
            }
        }
    }
}
