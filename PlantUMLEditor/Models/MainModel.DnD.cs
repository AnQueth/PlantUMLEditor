using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
        private TreeViewModel? _selectedFile;
        private TreeViewModel? _selectedFolder;

        public void TextDragOver(object sender, DragEventArgs e)
        {
            if (CurrentDocument is not null)
            {
                if (string.Equals(Path.GetExtension(CurrentDocument.FileName), FileExtension.MD, StringComparison.OrdinalIgnoreCase))
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
                if (string.Equals(Path.GetExtension(CurrentDocument.FileName), FileExtension.MD, StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = (string)e.Data.GetData(DataFormats.StringFormat);
                    if (CurrentDocument is TextDocumentModel tdm)
                    {
                        string name = Path.GetFileNameWithoutExtension(fileName);
                        var folder = Path.GetDirectoryName(CurrentDocument.FileName);
                        if (folder is null)
                        {
                            folder = "";
                        }
                        fileName = Path.GetRelativePath(folder, fileName);
                        string extension = Path.GetExtension(fileName);

                        fileName = fileName.Replace('\\', '/');

                        if (extension.Equals(FileExtension.PNG, StringComparison.OrdinalIgnoreCase) || extension.Equals(FileExtension.JPG, StringComparison.OrdinalIgnoreCase))
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
                            System.Diagnostics.Debug.WriteLine(ex);
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
                    CreateUMLPngImage.RaiseCanExecuteChanged();
                    CreateUMLSVGImage.RaiseCanExecuteChanged();
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

        private async void NewURLLinkDiagramHandler()
        {
            await _newFileManager.CreateNewURLLinkFile(_selectedFolder, FolderBase);
        }

        private async void NewUnknownDiagramHandler()
        {
            await _newFileManager.CreateNewUnknownDiagramFile(_selectedFolder, FolderBase);
        }

        private async void NewYAMLDocumentHandler()
        {
            await _newFileManager.CreateNewYamlFile(_selectedFolder, FolderBase);
        }
    }
}

