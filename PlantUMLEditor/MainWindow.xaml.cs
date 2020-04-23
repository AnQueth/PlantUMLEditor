using PlantUMLEditor.Models;
using PlantUMLEditor.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlantUMLEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
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

        private void RichTextBox_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void RichTextBox_Unloaded(object sender, RoutedEventArgs e)
        {

        }
    }
}