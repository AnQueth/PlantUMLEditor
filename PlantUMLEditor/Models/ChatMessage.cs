using Markdig;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Documents;
using static PlantUMLEditor.Models.MainModel;

namespace PlantUMLEditor.Models
{
    public class ChatMessage(bool isUser) : INotifyPropertyChanged
    {

        private bool _isBusy = true;
        public bool IsBusy
        {
            get
            {
                return _isBusy;
            }
            set
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
            }
        }

        public class ToolCall
        {
            // allow nullable since some tool result objects may not have all fields
            public string? Id { get; set; }
            public string? ToolName { get; set; }
            public string? Arguments { get; set; }
            public string? Result { get; set; }

        }

        public bool IsUser { get; } = isUser;

        public ObservableCollection<ToolCall> ToolCalls { get; } = new ObservableCollection<ToolCall>();

        public FlowDocument Document
        {
            get
            {
                MarkdownPipeline? pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

                var renderer = new ChatWpfRenderer();
                var doc = Markdig.Wpf.Markdown.ToFlowDocument(Message, pipeline, renderer);
                var xaml = Markdig.Wpf.Markdown.ToXaml(Message, pipeline);

               // FlowDocumentCompactor.CompactFlowDocument(doc);
            

                return doc;

            }
        }

        /// <summary>
        /// Custom WPF renderer for chat messages using Markdig
        /// </summary>
        private class ChatWpfRenderer : Markdig.Renderers.WpfRenderer
        {
            public ChatWpfRenderer() : base()
            {
            }
        }

        public ObservableCollection<UndoOperation> Undos { get; init; } = new ObservableCollection<UndoOperation>();

        private string _message = string.Empty;
        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged(nameof(Message));
                if (!string.IsNullOrEmpty(_message))
                {


                    OnPropertyChanged(nameof(Document));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}