using PlantUMLEditor.Controls;
using System.Windows;

namespace PlantUMLEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            UMLColorCodingConfig.LoadFromSettings();
            MDColorCodingConfig.LoadFromSettings();
            base.OnStartup(e);
        }
    }
}