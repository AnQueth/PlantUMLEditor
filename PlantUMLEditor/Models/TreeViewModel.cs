using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace PlantUMLEditor.Models
{
    public class TreeViewModel : BindingBase
    {
        private readonly IFolderChangeNotifactions _folderChangeNotifactions;
        private bool _isRenaming = false;
        private Visibility _isVisible;
        private string _name;
        private string _rename;

        public TreeViewModel(string path, bool isFile, string icon, IFolderChangeNotifactions folderChangeNotifactions)
        {
            FullPath = path;
            Icon = icon;
            Name = Path.GetFileName(path);
            IsFile = isFile;
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

        public DelegateCommand DeleteCommand { get; }

        public DelegateCommand DoRenameCommand { get; }

        public string FullPath
        {
            get; set;
        }

        public string Icon { get; private set; }

        public bool IsFile
        {
            get; set;
        }

        public bool IsRenaming
        {
            get
            {
                return _isRenaming;
            }
            set
            {
                SetValue(ref _isRenaming, value);
            }
        }

        public bool IsSelected { get; set; }

        public Visibility IsVisible
        {
            get
            {
                return _isVisible;
            }
            set
            {
                SetValue(ref _isVisible, value);
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                SetValue(ref _name, value);
            }
        }

        public DelegateCommand NewFolderCommand { get; }

        public string Rename
        {
            get
            {
                return _rename;
            }
            set
            {
                SetValue(ref _rename, value);
            }
        }

        public DelegateCommand StartRenameCommand { get; }

        private void DeleteCommandHandler()
        {
            if (MessageBoxResult.No == MessageBox.Show(string.Format("Delete {0}", this.FullPath), "Delete?", MessageBoxButton.YesNo))
                return;

            if (File.Exists(this.FullPath))
            {
                File.Delete(this.FullPath);
            }
            this.IsVisible = Visibility.Collapsed;
        }

        private void NewFolderHandler()
        {
            string nf = Path.Combine(this.FullPath, "New Folder");
            if (!Directory.Exists(nf))
                Directory.CreateDirectory(nf);

            Children.Add(new TreeViewModel(nf, false, "", _folderChangeNotifactions)
            {
                IsRenaming = true,
                Rename = "New Folder"
            });
        }

        private void RenameCommandHandler()
        {
            if (string.IsNullOrEmpty(Rename))
                return;
            if (!this.IsFile)
            {
                string nf = Path.Combine(Path.GetDirectoryName(this.FullPath), Rename);
                if (Directory.Exists(nf))
                    return;
                try
                {
                    Directory.Move(this.FullPath, nf);
                    this.FullPath = nf;
                    this.Name = Rename;

                    IsRenaming = false;
                    Name = Rename;
                }
                catch
                {
                }
            }
            else
            {
                string nf = Path.Combine(Path.GetDirectoryName(this.FullPath), Rename);
                if (File.Exists(nf))
                    return;
                File.Move(this.FullPath, nf);
                this.FullPath = nf;
                this.Name = Rename;

                IsRenaming = false;
                Name = Rename;
            }
            _folderChangeNotifactions.Change(this.FullPath);
        }

        private void StartRenameHandler()
        {
            IsRenaming = true;
            Rename = Name;
        }
    }
}