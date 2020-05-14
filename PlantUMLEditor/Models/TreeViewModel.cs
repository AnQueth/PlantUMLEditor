using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Forms;

namespace PlantUMLEditor.Models
{
    public class TreeViewModel : BindingBase
    {
        private readonly IFolderChangeNotifactions _folderChangeNotifactions;
        private bool _isRenaming = false;
        private string _name;

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
            _folderChangeNotifactions = folderChangeNotifactions;
        }

        public ObservableCollection<TreeViewModel> Children
        {
            get;
        }

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
            internal set
            {
                SetValue(ref _isRenaming, value);
            }
        }

        public bool IsSelected { get; set; }

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
            get;
            set;
        }

        public DelegateCommand StartRenameCommand { get; }

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