using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PlantUMLEditor
{
    /// <summary>
    /// Interaction logic for MDColorCodingConfigWindow.xaml
    /// </summary>
    public partial class MDColorCodingConfigWindow : Window
    {
        public MDColorCodingConfigWindow()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            PlantUMLEditor.Controls.MDColorCodingConfig.SaveToSettings();
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
