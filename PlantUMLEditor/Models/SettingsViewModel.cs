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

        public bool UseTrueUMLGenMode
        {
            get => AppSettings.Default.UMLPureMode;
            set
            {
                AppSettings.Default.UMLPureMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseTrueUMLGenMode)));
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

        public string AzureAIEndpoint
        {
            get => AppSettings.Default.AzureAIEndpoint;
            set
            {
                AppSettings.Default.AzureAIEndpoint = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AzureAIEndpoint)));
            }
        }


        public string AzureAIKey
        {
            get => AppSettings.Default.AzureAIKey;
            set
            {
                AppSettings.Default.AzureAIKey = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AzureAIKey)));
            }
        }


        public string AzureAIDeployment
        {
            get => AppSettings.Default.AzureAIDeployment;
            set
            {
                AppSettings.Default.AzureAIDeployment = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AzureAIDeployment)));
            }
        }


        public int AzureAIMaxOutputTokens
        {
            get => AppSettings.Default.AzureAIMaxOutputTokens;
            set
            {
                AppSettings.Default.AzureAIMaxOutputTokens = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AzureAIMaxOutputTokens)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
