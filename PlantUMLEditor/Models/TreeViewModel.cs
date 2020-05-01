using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Forms;

namespace PlantUMLEditor.Models
{
    public class TreeViewModel : BindingBase
    {
        private string _name;

        public TreeViewModel(string path, bool isFile, string icon)
        {
            FullPath = path;
            Icon = icon;
            IsFile = isFile;
            Children = new ObservableCollection<TreeViewModel>();
        }

        public string Icon { get; private set; }

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