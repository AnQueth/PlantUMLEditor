using System.Collections.ObjectModel;
using System.IO;

namespace PlantUMLEditor.Models
{
    public class FolderBindingModel : BindingBase
    {
        private string _name;

        public FolderBindingModel(string path, bool isFile)
        {
            FullPath = path;
            Name = Path.GetDirectoryName(path);
            IsFile = isFile;
            Children = new ObservableCollection<FolderBindingModel>();
        }

        public bool IsSelected { get; set; }

        public string FullPath
        {
            get; set;
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

        public bool IsFile
        {
            get; set;
        }

        public ObservableCollection<FolderBindingModel> Children
        {
            get;
        }
    }
}