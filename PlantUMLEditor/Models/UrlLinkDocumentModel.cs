using Microsoft.Xaml.Behaviors.Core;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PlantUMLEditor.Models
{
    internal class UrlLinkDocumentModel : BaseDocumentModel, ITextGetter, IScriptable
    {
        private string _url;

        private Func<Task<string>>? _captureHtmlFunc;

        public UrlLinkDocumentModel(string fileName, string title, string url) : base(fileName, title)
        {
            _url = url;

        }

        public string URL
        {
            get => _url;
            set => SetValue(ref _url, value);
        }

        private Func<string, Task< string>>? _scriptExecuter;

        internal Func<string, Task<string>>? ScriptExecuter
        {
            get => _scriptExecuter;
            set => _scriptExecuter = value;

        }



        /// <summary>
        /// Internal function set by the behavior to capture HTML on demand
        /// </summary>
        internal Func<Task<string>>? CaptureHtmlFunc
        {
            get => _captureHtmlFunc;
            set => _captureHtmlFunc = value;
        }

        private async Task<string> CaptureAndUpdateContentAsync()
        {
            try
            {
                if (_captureHtmlFunc != null)
                {
                    string html = await _captureHtmlFunc();
                    return html;
                }


            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error capturing HTML: {ex.Message}");
            }

            return string.Empty;
        }

        public async Task<string> ExecuteScript(string script)
        {
            try
            {
                if (_scriptExecuter != null)
                {
                    return await _scriptExecuter(script);
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing script: {ex.Message}");
                return ex.ToString();
            }
        }

        public async Task<string> ReadContent()
        {
            return await CaptureAndUpdateContentAsync();
        }
    }
}