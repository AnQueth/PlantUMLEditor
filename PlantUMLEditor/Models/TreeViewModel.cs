using Prism.Commands;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace PlantUMLEditor.Models
{
    public class FolderTreeViewModel : TreeViewModel
    {
        private bool _isExpanded;
        public FolderTreeViewModel(TreeViewModel? parent, string path, bool isExpanded) :
            base(parent, path, "pack://application:,,,/PlantUMLEditor;component/images/FolderClosed_16x.png")
        {
            IsFile = false;
            IsExpanded = isExpanded;

            NewFolderCommand = new DelegateCommand(NewFolderHandler);
        }

        protected override void DoRename()
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

        public DelegateCommand NewFolderCommand
        {
            get;
        }

        private void NewFolderHandler()
        {
            string nf = Path.Combine(FullPath, "New Folder");
            if (!Directory.Exists(nf))
            {
                Directory.CreateDirectory(nf);
            }

            Children.Add(new FolderTreeViewModel(this, nf, true)
            {
                IsRenaming = true,
                Rename = "New Folder"
            });
        }


        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                SetValue(ref _isExpanded, value);

                var f = new FoldersStatusPersistance();

                if (value)
                {
                    f.SaveOpenFolders(FullPath);
                }
                else
                {
                    f.SaveClosedFolders(FullPath);
                }
            }
        }
    }

    public class TreeViewModel : BindingBase
    {

        private bool _isRenaming = false;
        private Visibility _isVisible;
        private string _name;
        private string _rename = string.Empty;

        public TreeViewModel(TreeViewModel? parent, string path, string icon)
        {
            Parent = parent;
            FullPath = path;

            Icon = icon;
            _name = Path.GetFileName(path);
            IsFile = true;

            if (IsFile)
            {
                IsUML = string.Equals(Path.GetExtension(path), ".puml", System.StringComparison.OrdinalIgnoreCase);
            }


            Children = new ObservableCollection<TreeViewModel>();

            StartRenameCommand = new DelegateCommand(StartRenameHandler);
            DoRenameCommand = new DelegateCommand(RenameCommandHandler);
            DeleteCommand = new DelegateCommand(DeleteCommandHandler);

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


        public bool IsRenaming
        {
            get => _isRenaming;
            set => SetValue(ref _isRenaming, value);
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


        protected virtual void DoRename()
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


        private void RenameCommandHandler()
        {
            if (string.IsNullOrEmpty(Rename))
            {
                return;
            }

            DoRename();







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