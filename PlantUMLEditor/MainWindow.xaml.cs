using PlantUMLEditor.Models;
using PlantUMLEditor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PlantUMLEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow :  Window
    {
        public MainWindow()
        {
            InitializeComponent();
           

            DataContext = new MainModel(new OpenDirectoryService(), new UMLDocumentCollectionSerialization());



        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           
            //myAdornerLayer = AdornerLayer.GetAdornerLayer(myTextBox);
            //myAdornerLayer.Add(new SimpleCircleAdorner(myTextBox));
        }

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {


        }
    }
}
