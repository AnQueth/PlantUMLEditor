using PlantUMLEditor.Controls;
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
    /// Interaction logic for UMLColorCodingConfig.xaml
    /// </summary>
    public partial class UMLColorCodingConfigWindow : Window
    {
        public UMLColorCodingConfigWindow()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            UMLColorCodingConfig.SaveToSettings();
            Close();
        }
    }
}
