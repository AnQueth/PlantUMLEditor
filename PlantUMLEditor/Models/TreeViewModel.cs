using Prism.Commands;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace PlantUMLEditor.Models
{
    public class TreeViewModel : BindingBase
    {
        private readonly IFolderChangeNotifactions _folderChangeNotifactions;
        private bool _isRenaming = false;
        private Visibility _isVisible;
        private string _name;
        private string _rename = string.Empty;
        private bool _isExpanded;

        public TreeViewModel(TreeViewModel? parent, string path, bool isFile, string icon,
            IFolderChangeNotifactions folderChangeNotifactions)
        {
            Parent = parent;
            FullPath = path;
            IsExpanded = true;
            Icon = icon;
            _name = Path.GetFileName(path);
            IsFile = isFile;
            if (isFile)
            {
                IsUML = string.Equals(Path.GetExtension(path), ".puml", System.StringComparison.OrdinalIgnoreCase);
            }

            if (!isFile)
            {
                Icon = "images\\FolderClosed_16x.png";
            }
            Children = new ObservableCollection<TreeViewModel>();
            NewFolderCommand = new DelegateCommand(NewFolderHandler);
            StartRenameCommand = new DelegateCommand(StartRenameHandler);
            DoRenameCommand = new DelegateCommand(RenameCommandHandler);
            DeleteCommand = new DelegateCommand(DeleteCommandHandler);
            _folderChangeNotifactions = folderChangeNotifactions;
        }

        public ObservableCollection<TreeViewModel> Children
        {
            get;
        }

        public DelegateCommand DeleteCommand
        {
            get;
        }

        public DelegateCommand DoRenameCommand
        {
            get;
        }

        public string FullPath
        {
            get; set;
        }

        public string Icon
        {
            get; private set;
        }

        public bool IsFile
        {
            get; set;
        }
        public bool IsUML
        {
            get;
            set;

        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetValue(ref _isExpanded, value);
        }
        public bool IsRenaming
        {
            get => _isRenaming;
            set => SetValue(ref _isRenaming, value);
        }

        public bool IsSelected
        {
            get; set;
        }

        public Visibility IsVisible
        {
            get => _isVisible;
            set => SetValue(ref _isVisible, value);
        }

        public string Name
        {
            get => _name;
            set => SetValue(ref _name, value);
        }

        public DelegateCommand NewFolderCommand
        {
            get;
        }
        public TreeViewModel? Parent
        {
            get;
        }

        public string Rename
        {
            get => _rename;
            set => SetValue(ref _rename, value);
        }

        public DelegateCommand StartRenameCommand
        {
            get;
        }

        private void DeleteCommandHandler()
        {
            if (MessageBoxResult.No == MessageBox.Show(
                string.Format(CultureInfo.InvariantCulture, "Delete {0}", FullPath), "Delete?", MessageBoxButton.YesNo))
            {
                return;
            }


            if (File.Exists(FullPath))
            {
                try
                {

                    File.Delete(FullPath);
                }
                catch
                {
                    return;
                }
            }
            IsVisible = Visibility.Collapsed;
            Parent?.Children.Remove(this);
        }

        private void NewFolderHandler()
        {
            string nf = Path.Combine(FullPath, "New Folder");
            if (!Directory.Exists(nf))
            {
                Directory.CreateDirectory(nf);
            }

            Children.Add(new TreeViewModel(this, nf, false, "", _folderChangeNotifactions)
            {
                IsRenaming = true,
                Rename = "New Folder"
            });
        }

        private async void RenameCommandHandler()
        {
            if (string.IsNullOrEmpty(Rename))
            {
                return;
            }

            if (!IsFile)
            {
                string? dir = Path.GetDirectoryName(FullPath);
                if (dir == null)
                {
                    return;
                }

                string nf = Path.Combine(dir, Rename);
                if (Directory.Exists(nf))
                {
                    return;
                }

                try
                {
                    Directory.Move(FullPath, nf);
                    FullPath = nf;

                    IsRenaming = false;
                    Name = Rename;
                }
                catch
                {
                }
            }
            else
            {
                var words = Name.Split('.');
                words[0] = Rename;
                Rename = string.Join('.', words);
                string? dir = Path.GetDirectoryName(FullPath);
                if (dir == null)
                {
                    return;
                }

                string nf = Path.Combine(dir, Rename);
                if (File.Exists(nf))
                {
                    return;
                }

                File.Move(FullPath, nf);
                FullPath = nf;

                IsRenaming = false;
                Name = Rename;
            }
            await _folderChangeNotifactions.Change(FullPath);
        }

        private void StartRenameHandler()
        {
            IsRenaming = true;
            if (IsFile)
            {
                Rename = Name.Split('.').First();
            }
            else
            {
                Rename = Name;
            }
        }
    }
}