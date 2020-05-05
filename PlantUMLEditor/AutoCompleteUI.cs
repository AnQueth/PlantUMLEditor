using PlantUMLEditor.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace PlantUMLEditor
{
    public class AutoCompleteUI : IAutoComplete
    {
        private ListBox _cb;
        private IAutoCompleteCallback _currentCallback = null;
        private bool _isVisible;
        private DependencyObject tabs;

        public AutoCompleteUI(DependencyObject tabs)
        {
            this.tabs = tabs;
        }

        public bool IsVisible
        {
            get => _isVisible;
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
            _isVisible = false;

            var g = FindVisualChild<Grid>(tabs);
            g.Visibility = Visibility.Collapsed;
        }

        public void FocusAutoComplete(Rect rec, IAutoCompleteCallback autoCompleteCallback, bool allowTyping)
        {
            _isVisible = true;
            _currentCallback = autoCompleteCallback;

            var g = FindVisualChild<Grid>(tabs);

            g.Visibility = Visibility.Visible;

            Canvas.SetLeft(g, rec.BottomRight.X);
            Canvas.SetTop(g, rec.BottomRight.Y);

            _cb = (ListBox)g.Children[0];

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
            _cb.SelectionChanged += AutoCompleteItemSelected;
        }

        public void SendEvent(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                this.CloseAutoComplete();

            int index = _cb.SelectedIndex;

            if (e.Key == Key.Up)
            {
                index--;
            }
            else if (e.Key == Key.Down)
            {
                index++;
            }

            if (index < 0)
                index = 0;
            if (index > _cb.Items.Count - 1)
                index = _cb.Items.Count - 1;
            Debug.WriteLine(index);
            _cb.SelectedIndex = index;

            _cb.ScrollIntoView(_cb.SelectedItem);
        }
    }
}