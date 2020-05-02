using PlantUMLEditor.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PlantUMLEditor
{
    public class AutoCompleteUI : IAutoComplete
    {
        private IAutoCompleteCallback _currentCallback = null;
        private DependencyObject tabs;

        public AutoCompleteUI(DependencyObject tabs)
        {
            this.tabs = tabs;
        }

        private void AutoCompleteItemSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string currentSelected = e.AddedItems[0] as string;

                _currentCallback.Selection(currentSelected);
            }
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

        private void NewItemClicked(object sender, RoutedEventArgs e)
        {
            var g = FindVisualChild<Grid>(tabs);
            var b = (TextBox)g.Children[1];
            _currentCallback.NewAutoComplete(b.Text);
        }

        public void CloseAutoComplete()
        {
            var g = FindVisualChild<Grid>(tabs);
            g.Visibility = Visibility.Collapsed;
        }

        public void FocusAutoComplete(Rect rec, IAutoCompleteCallback autoCompleteCallback, bool allowTyping)
        {
            _currentCallback = autoCompleteCallback;

            var g = FindVisualChild<Grid>(tabs);

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

            b.Click += NewItemClicked;
            cb.SelectionChanged += AutoCompleteItemSelected;
        }
    }
}