using PlantUMLEditor.Controls;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class JsonDocumentModel : TextDocumentModel
    {
        private readonly Action<UMLDiagram, UMLDiagram> ChangedCallback;


        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {
            PreviewDiagramModel? imageModel = new PreviewDiagramModel(base._ioService, base._jarLocation, Title);

            Preview? previewWindow = new Preview
            {
                DataContext = imageModel
            };

            return (imageModel, previewWindow);

        }

        public JsonDocumentModel(Action<UMLDiagram, UMLDiagram> changedCallback, IConfiguration configuration,
                        IIOService openDirectoryService,
                        UMLUnknownDiagram model, UMLDocumentCollection diagrams, string fileName, string title, string content,
                         AutoResetEvent messageCheckerTrigger
                        ) : base(configuration, openDirectoryService, fileName, title, content, messageCheckerTrigger)
        {
            ChangedCallback = changedCallback;
            Diagram = model;
            Diagrams = diagrams;
            colorCodingProvider = new UMLColorCoding();
            indenter = new Indenter();
        }

        public UMLUnknownDiagram Diagram
        {
            get; internal set;
        }
        public UMLDocumentCollection Diagrams
        {
            get; internal set;
        }

        private readonly Indenter indenter;

        private readonly UMLColorCoding colorCodingProvider;

        protected override async Task ContentChanged(string text)
        {
            _ = Task.Factory.StartNew(() =>
            {
                Diagram.LineErrors.Clear();
                int lineoffset = 0;
                try
                {


                    var start = text.IndexOf('{');
                    var end = text.LastIndexOf('}');
                    for (var x = 0; x < start; x++)
                    {
                        if (text[x] == '\n')
                        {
                            lineoffset++;

                        }
                    }
                    if (start >= 0 && end >= 0)
                    {
                        var t = text.AsSpan().Slice(start, end - start + 1);
                        _ = JsonSerializer.Deserialize<object>(t);
                    }


                }
                catch (JsonException ex)
                {
                    if (ex.LineNumber == null)
                    {
                        Diagram.AddLineError(ex.Message, 0);
                    }
                    else
                    {
                        Diagram.AddLineError(ex.Message, lineoffset + (int)ex.LineNumber);
                    }

                }
                catch (Exception ex)
                {
                    Diagram.AddLineError(ex.Message, 0);
                }
            });





            await base.ContentChanged(text);
        }

        protected override IIndenter GetIndenter()
        {
            return indenter;
        }

        protected override IColorCodingProvider? GetColorCodingProvider()
        {
            return colorCodingProvider;
        }

        public override Task<UMLDiagram?> GetEditedDiagram()
        {
            return Task.FromResult<UMLDiagram?>(Diagram);
        }

        internal override void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {

        }
    }
}