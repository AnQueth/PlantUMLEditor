﻿using System.Windows;
using System.Windows.Controls;

namespace PlantUMLEditor
{
    internal class TabHeaderDataTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            FrameworkElement element = container as FrameworkElement;
            if (item is Models.TextDocumentModel)
            {
                return (DataTemplate)element.FindResource("TextDocumentTabHeader");
            }
            else if (item is Models.ImageDocumentModel)
            {
                return (DataTemplate)element.FindResource("ImageDocumentTabHeader");
            }
            return base.SelectTemplate(item, container);
        }
    }
}
