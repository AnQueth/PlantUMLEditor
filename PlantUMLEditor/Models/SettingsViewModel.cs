using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        public string DocFXEXE
        {
            get => AppSettings.Default.DocFXEXE;
            set
            {
                AppSettings.Default.DocFXEXE = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DocFXEXE)));
            }
        }

        public string GITUser
        {
            get => AppSettings.Default.GITUser;
            set
            {
                AppSettings.Default.GITUser = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GITUser)));
            }
        }

        public string GITEmail
        {
            get => AppSettings.Default.GITEmail;
            set
            {
                AppSettings.Default.GITEmail = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GITEmail)));
            }

        }

        public string PlantUMLJarLocation
        {
            get => AppSettings.Default.JARLocation;
            set
            {
                AppSettings.Default.JARLocation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlantUMLJarLocation)));
            }

        }

       public string TemplatesDirectory
        {
            get => AppSettings.Default.TemplatePath;
            set
            {
                AppSettings.Default.TemplatePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TemplatesDirectory)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
