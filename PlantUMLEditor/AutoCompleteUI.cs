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
    public class AutoCompleteUI
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
    }
}