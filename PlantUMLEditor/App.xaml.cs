using PlantUMLEditor.Controls;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

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