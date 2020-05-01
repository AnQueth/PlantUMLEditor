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
    public partial class MainWindow : Window, IAutoComplete
    {
        public MainWindow()
        {
            InitializeComponent();
            _model = new MainModel(new OpenDirectoryService(), new UMLDocumentCollectionSerialization(), this);
            DataContext = _model;
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

        private bool setEventHandler = false;
        private readonly MainModel _model;
        private IAutoCompleteCallback _currentCallback = null;
        public void FocusAutoComplete(Rect rec, IAutoCompleteCallback autoCompleteCallback, bool allowTyping)
        {
            _currentCallback = autoCompleteCallback;
        

      

            var g = FindVisualChild<Grid>(Tabs);

            g.Visibility = Visibility.Visible;

            Canvas.SetLeft(g, rec.BottomRight.X);
            Canvas.SetTop(g, rec.BottomRight.Y);

            var cb = (ListBox)g.Children[0];
         
            var t = (TextBox)g.Children[1];
            var b = (Button)g.Children[2];
          

            if (allowTyping)
            {
                t.Visibility = Visibility.Visible;
                b.Visibility = Visibility.Visible;
            }
            else
            {
                t.Visibility = Visibility.Collapsed;
                b.Visibility = Visibility.Collapsed;
            }

            //cb.Focus();
          
                b.Click += B_Click;
                cb.SelectionChanged += Cb_SelectionChanged;
            
            setEventHandler = true;
        }

    

        private void B_Click(object sender, RoutedEventArgs e)
        {
            var g = FindVisualChild<Grid>(Tabs);
            var b = (TextBox)g.Children[1];
            _currentCallback.NewAutoComplete(b.Text);
        }

        private childItem FindVisualChild<childItem>(DependencyObject obj)
   where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem && (string)child.GetValue(FrameworkElement.NameProperty) == "AutoCompleteGrid")
                {
                    return (childItem)child;
                }
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        private void Cb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string currentSelected = e.AddedItems[0] as string;

                _currentCallback.Selection(currentSelected);

            }
        }

        public void CloseAutoComplete()
        {
            var g = FindVisualChild<Grid>(Tabs);
            g.Visibility = Visibility.Collapsed;
        }
    }
}