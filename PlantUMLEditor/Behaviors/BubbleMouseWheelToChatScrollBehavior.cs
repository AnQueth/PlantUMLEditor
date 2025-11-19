using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlantUMLEditor.Behaviors
{
    /// <summary>
    /// Behavior that listens for PreviewMouseWheel (handledEventsToo) on the associated element
    /// and forwards scrolling to the named ChatScroll ScrollViewer on the Window.
    /// Attach this to the inner FlowDocumentScrollViewer in the chat item template.
    /// </summary>
    public class BubbleMouseWheelToChatScrollBehavior : Behavior<FrameworkElement>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewMouseWheel), true);
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.RemoveHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewMouseWheel));
            }
            base.OnDetaching();
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                var window = Window.GetWindow(AssociatedObject);
                if (window == null)
                    return;

                var chatScroll = window.FindName("ChatScroll") as ScrollViewer;
                if (chatScroll == null)
                    return;

                // Determine scroll amount; make it reasonable
                double offsetChange = e.Delta / 3.0;
                double target = chatScroll.VerticalOffset - offsetChange;

                if (target < 0)
                    target = 0;
                if (target > chatScroll.ScrollableHeight)
                    target = chatScroll.ScrollableHeight;

                chatScroll.ScrollToVerticalOffset(target);

                // mark handled so inner viewer won't double-handle
                e.Handled = true;
            }
            catch
            {
                // swallow exceptions to avoid breaking UI
            }
        }
    }
}
