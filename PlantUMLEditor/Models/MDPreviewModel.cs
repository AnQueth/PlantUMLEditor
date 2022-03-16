using Markdig.Wpf;
using Prism.Mvvm;
using System;
using System.IO;
using System.Windows.Documents;

namespace PlantUMLEditor.Models
{
    internal class MDPreviewModel : BindableBase, IPreviewModel
    {
        private FlowDocument _document;
        internal class LinkInlineRenderer : Markdig.Renderers.Wpf.Inlines.LinkInlineRenderer
        {
            private readonly Uri _linkpath;

            public LinkInlineRenderer(string linkpath)
            {
                _linkpath = new Uri(linkpath);
            }

            protected override void Write(Markdig.Renderers.WpfRenderer renderer,
                Markdig.Syntax.Inlines.LinkInline link)
            {
                if (link?.IsImage ?? throw new ArgumentNullException(nameof(link)))
                {
                    if (!new Uri(link.Url, UriKind.RelativeOrAbsolute).IsAbsoluteUri)
                    {
                        Uri u = new Uri(_linkpath, link.Url);
                        link.Url = u.AbsoluteUri;
                    }
                }

                base.Write(renderer, link);
            }
        }

        internal class WpfRenderer : Markdig.Renderers.WpfRenderer
        {
            private readonly string _linkpath;

            /// <summary>
            /// Initializes the WPF renderer
            /// </summary>
            /// <param name="linkpath">image path for the custom LinkInlineRenderer</param>
            public WpfRenderer(string linkpath) : base()
            {
                _linkpath = linkpath;
            }

            /// <summary>
            /// Load first the custom renderer's
            /// </summary>
            protected override void LoadRenderers()
            {
                ObjectRenderers.Add(new LinkInlineRenderer(_linkpath));
                base.LoadRenderers();
            }
        }

        public string Title
        {
            get;
            init;
        }

        private readonly string _workingDirectory;

        public MDPreviewModel(string title, string workingDirectory)
        {
            Title = title;
            if (!workingDirectory.EndsWith(Path.DirectorySeparatorChar))
            {
                workingDirectory += Path.DirectorySeparatorChar;
            }

            _workingDirectory = workingDirectory;
        }

        public FlowDocument Document
        {
            get => _document;
            set => SetProperty(ref _document, value);
        }

        public void Show(string path, string name, bool delete)
        {


            var r = new WpfRenderer(_workingDirectory);

            var markdown = File.ReadAllText(path);
            Document = Markdown.ToFlowDocument(markdown, null, r);


        }


        public void Stop()
        {

        }
    }
}
