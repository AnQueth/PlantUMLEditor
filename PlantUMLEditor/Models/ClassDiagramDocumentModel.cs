using PlantUML;
using PlantUMLEditor.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class ClassDiagramDocumentModel : TextDocumentModel
    {

        private IEnumerable<string> _autoCompleteItems = Array.Empty<string>();
        private readonly object _locker = new();
        private bool _running = true;

        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {
            PreviewDiagramModel? imageModel = new PreviewDiagramModel(base._ioService, base._jarLocation, Title);

            Preview? previewWindow = new Preview
            {
                DataContext = imageModel
            };

            return (imageModel, previewWindow);

        }

        protected override IColorCodingProvider? GetColorCodingProvider()
        {
            return colorCodingProvider;
        }

        public ClassDiagramDocumentModel(IConfiguration configuration,
            IIOService openDirectoryService,

            UMLClassDiagram diagram,
            LockedList<UMLClassDiagram> otherClassDiagrams,
            string fileName,
            string title,
            string content, AutoResetEvent messageCheckerTrigger) : base(configuration, openDirectoryService,
                fileName, title, content, messageCheckerTrigger)
        {
            Diagram = diagram;

            colorCodingProvider = new UMLColorCoding();

            _ = Task.Run(async () =>
            {
                while (_running)
                {
                    await Task.Delay(1000);

                    if (IsDirty)
                    {


                        UMLClassDiagram? z = (await GetEditedDiagram()) as UMLClassDiagram;
                        if (z != null)
                        {
                            lock (_locker)
                            {
                                try
                                {
                                    _autoCompleteItems = z.DataTypes.Select(p => p.Name).Union(otherClassDiagrams.SelectMany(z => z.DataTypes).Select(z => z.Name)).ToArray();
                                }
                                catch
                                {

                                }
                            }
                        }
                    }
                }
            });
            indenter = new Indenter();
        }

        protected override IIndenter GetIndenter()
        {
            return indenter;
        }

        public UMLClassDiagram Diagram
        {
            get; private set;
        }

        private readonly UMLColorCoding colorCodingProvider;
        private readonly Indenter indenter;

        protected override void RegenDocumentHandler()
        {
            string t = PlantUMLGenerator.Create(Diagram);

            TextEditor?.TextWrite(t, true, GetColorCodingProvider(), GetIndenter());

            base.RegenDocumentHandler();
        }

        internal void UpdateDiagram(UMLClassDiagram doc)
        {
            if (TextEditor == null)
            {
                _bindedAction = () =>
                {
                    if (TextEditor != null)
                    {
                        Content = PlantUMLGenerator.Create(doc);
                        //TextEditor.TextWrite(PlantUMLGenerator.Create(doc), true, GetColorCodingProvider());
                    }
                };
            }
            else
            {
                Content = PlantUMLGenerator.Create(doc);
                // TextEditor.TextWrite(PlantUMLGenerator.Create(doc), true, GetColorCodingProvider());
            }
            IsDirty = true;
        }

        internal override void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {

            base.MatchingAutoCompletes.Clear();





            lock (_locker)
            {
                if (!string.IsNullOrEmpty(autoCompleteParameters.TypedWord)
                    && !autoCompleteParameters.TypedWord.EndsWith("<", StringComparison.Ordinal))
                {
                    foreach (string? item in _autoCompleteItems.Where(p => p.StartsWith(autoCompleteParameters.TypedWord, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        base.MatchingAutoCompletes.Add(item);
                    }
                }
            }
            if (MatchingAutoCompletes.Count > 0)
            {
                base.ShowAutoComplete();
            }
            else
            {
                base.CloseAutoComplete();
            }
        }

        public override void Close()
        {
            _running = false;
            base.Close();
        }


        private UMLDiagram? _diagram;

        public override async Task<UMLDiagram?> GetEditedDiagram()
        {



            if (DocGeneratorDirty || _diagram is null)
            {

                if (Content is null)
                {
                    return null;
                }

                _diagram = await PlantUML.UMLClassDiagramParser.ReadString(Content);
                DocGeneratorDirty = false;
            }
            return _diagram;


        }
    }
}