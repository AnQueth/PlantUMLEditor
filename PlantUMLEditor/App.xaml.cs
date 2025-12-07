using PlantUMLEditor.Controls;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Windows.Media;

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

            // Apply saved font selection to application resources/merged dictionaries
            try
            {
                string? saved = AppSettings.Default.SelectedFont;
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    var ff = new FontFamily(saved);

                    void Update(ResourceDictionary dict)
                    {
                        if (dict == null) return;
                        if (dict.Contains("FontFamily.Primary"))
                        {
                            dict["FontFamily.Primary"] = ff;
                        }

                        if (dict.MergedDictionaries != null)
                        {
                            foreach (var md in dict.MergedDictionaries)
                            {
                                try { Update(md); } catch { }
                            }
                        }
                    }

                    Update(this.Resources);

                    // Ensure top-level resource exists so DynamicResource resolves
                    try
                    {
                        this.Resources["FontFamily.Primary"] = ff;
                    }
                    catch { }
                }
            }
            catch { }

            base.OnStartup(e);

        }
    }
}