using PlantUMLEditor.Models;
using System.Windows;
using System.Windows.Controls;

namespace PlantUMLEditor
{
    internal class TabHeaderDataTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            FrameworkElement? element = container as FrameworkElement;
            if (element is null)
            {
                return null;
            }

            if (item is Models.TextDocumentModel)
            {
                return (DataTemplate)element.FindResource("TextDocumentTabHeader");
            }
            else if (item is Models.ImageDocumentModel || item is UrlLinkDocumentModel || item is SVGDocumentModel)
            {
                return (DataTemplate)element.FindResource("ImageDocumentTabHeader");
            }
      
                return base.SelectTemplate(item, container);
        }
    }
}
