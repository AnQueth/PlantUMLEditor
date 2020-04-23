using System.Collections.ObjectModel;
using System.IO;

namespace PlantUMLEditor.Models
{
    public class TreeViewModel : BindingBase
    {
        private string _name;

        public TreeViewModel(string path, bool isFile)
        {
            FullPath = path;
     
            IsFile = isFile;
            Children = new ObservableCollection<TreeViewModel>();
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

        public ObservableCollection<TreeViewModel> Children
        {
            get;
        }
    }
}