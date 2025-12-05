using Microsoft.Xaml.Behaviors;
using Microsoft.Web.WebView2.Wpf;
using PlantUMLEditor.Models;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace PlantUMLEditor.Behaviors
{
    /// <summary>
    /// Behavior that captures HTML content from WebView2 on demand
    /// and stores it in the UrlLinkDocumentModel.Content property.
    /// HTML is captured when the Content property is accessed or when CaptureHtmlCommand is invoked.
    /// </summary>
    public class WebView2HtmlCaptureBehavior : Behavior<WebView2>
    {
        private UrlLinkDocumentModel? _model;

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.DataContextChanged += OnDataContextChanged;
                AssociatedObject.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
         

                // Set up the capture function if DataContext is already set
                SetupCaptureFunction();
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.DataContextChanged -= OnDataContextChanged;
                AssociatedObject.CoreWebView2InitializationCompleted -= OnCoreWebView2InitializationCompleted;
         
            }

            // Clean up the capture function
            if (_model != null)
            {
                _model.CaptureHtmlFunc = null;
                _model = null;
            }

            base.OnDetaching();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetupCaptureFunction();
        }

        private void OnCoreWebView2InitializationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
        {
            // Initialization completed, ready to capture HTML
        }
 

        private void SetupCaptureFunction()
        {
            // Clean up previous model
            if (_model != null)
            {
                _model.CaptureHtmlFunc = null;
            }

            // Setup new model
            if (AssociatedObject?.DataContext is UrlLinkDocumentModel model)
            {
                _model = model;
                _model.CaptureHtmlFunc = CaptureHtmlAsync;
            }
        }

        private async Task<string> CaptureHtmlAsync()
        {
            if (AssociatedObject?.CoreWebView2 != null)
            {
                try
                {
                    return await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {


                        // Execute JavaScript to get the HTML content
                        string html = await AssociatedObject.ExecuteScriptAsync("document.documentElement.outerText;");

                        // The result is JSON-encoded, so we need to unescape it
                        if (!string.IsNullOrEmpty(html))
                        {
                            // Remove the surrounding quotes and unescape the JSON string
                            return System.Text.Json.JsonSerializer.Deserialize<string>(html) ?? string.Empty;
                        }
                        return string.Empty;
                    }).Task.Unwrap();
                }
                catch (Exception )
                {
                    throw;
                }
            }

            return string.Empty;
        }
    }
}
