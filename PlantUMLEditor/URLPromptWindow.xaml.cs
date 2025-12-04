using System.Windows;

namespace PlantUMLEditor
{
    /// <summary>
    /// Interaction logic for URLPromptWindow.xaml
    /// </summary>
    public partial class URLPromptWindow : Window
    {
        public string? URLValue { get; private set; }

        public URLPromptWindow()
        {
            InitializeComponent();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            URLValue = URLTextBox.Text;
            DialogResult = true;
            Close();
        }
    }
}
