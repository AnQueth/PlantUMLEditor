using PlantUMLEditor.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace PlantUMLEditor
{
    public class AutoCompleteUI : IAutoComplete
    {
        private ListBox _cb;
        private IAutoCompleteCallback _currentCallback = null;

        private Lazy<Popup> popup;
        private DependencyObject tabs;

        public AutoCompleteUI(DependencyObject tabs)
        {
            popup = new Lazy<Popup>(() =>
            {
                var g = FindVisualChild<Popup>(tabs);
                if (g == null)
                    throw new ArgumentNullException(nameof(g));

                return g;
            }, System.Threading.LazyThreadSafetyMode.PublicationOnly);
        }

        public bool IsVisible
        {
            get
            {
                try
                {
                    return popup.Value.IsOpen;
                }
                catch { }
                return false;
            }
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
                if (child != null && child is childItem && (string)child.GetValue(FrameworkElement.NameProperty) == "AutoCompletePopup")
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

        public void CloseAutoComplete()
        {
            popup.Value.IsOpen = false;
        }

        public void FocusAutoComplete(Rect rec, IAutoCompleteCallback autoCompleteCallback, bool allowTyping)
        {
            _currentCallback = autoCompleteCallback;

            var g = popup.Value;

            g.IsOpen = true;
            g.Placement = PlacementMode.RelativePoint;
            g.HorizontalOffset = rec.Left;
            g.VerticalOffset = rec.Bottom;

            g.Visibility = Visibility.Visible;

            _cb = (ListBox)((Grid)g.Child).Children[0];

            _cb.SelectionChanged -= AutoCompleteItemSelected;
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