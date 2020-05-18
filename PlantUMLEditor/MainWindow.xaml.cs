using PlantUMLEditor.Models;
using PlantUMLEditor.Services;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Input;
using System.Windows.Media;

namespace PlantUMLEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainModel _model;

        private bool setEventHandler = false;

        public MainWindow()
        {
            InitializeComponent();
            _model = new MainModel(new OpenDirectoryService(), new UMLDocumentCollectionSerialization());
            DataContext = _model;
        }
    }
}