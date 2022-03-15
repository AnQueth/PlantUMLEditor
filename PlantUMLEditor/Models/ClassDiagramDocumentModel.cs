using PlantUML;
using PlantUMLEditor.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class ClassDiagramDocumentModel : DocumentModel
    {

        private IEnumerable<string> _autoCompleteItems = Array.Empty<string>();
        private readonly object _locker = new();
        private bool _running = true;

        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {
            var imageModel = new PreviewDiagramModel(base._ioService, base._jarLocation, Name);

            var previewWindow = new Preview
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
            string content) : base(configuration, openDirectoryService, DocumentTypes.Class,
                fileName, title, content)
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
                        string text = (string?)Application.Current.Dispatcher.Invoke((() =>
                      {
                          return TextEditor?.TextRead();
                      })) ?? string.Empty;

                        var z = await PlantUML.UMLClassDiagramParser.ReadString(text);
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
        }

        public UMLClassDiagram Diagram
        {
            get; private set;
        }

        private readonly UMLColorCoding colorCodingProvider;

        protected override void RegenDocumentHandler()
        {
            string t = PlantUMLGenerator.Create(Diagram);

            TextEditor?.TextWrite(t, true, GetColorCodingProvider());

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
                        TextEditor.TextWrite(PlantUMLGenerator.Create(doc), true, GetColorCodingProvider());
                    }
                };
            }
            else
            {
                TextEditor.TextWrite(PlantUMLGenerator.Create(doc), true, GetColorCodingProvider());
            }
        }

        internal override void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {

            base.MatchingAutoCompletes.Clear();





            lock (_locker)
            {
                if (!string.IsNullOrEmpty(autoCompleteParameters.WordStart)
                    && !autoCompleteParameters.WordStart.EndsWith("<", StringComparison.Ordinal))
                {
                    foreach (var item in _autoCompleteItems.Where(p => p.StartsWith(autoCompleteParameters.WordStart, StringComparison.InvariantCultureIgnoreCase)))
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

        public override async Task<UMLDiagram?> GetEditedDiagram()
        {
            return await PlantUML.UMLClassDiagramParser.ReadString(Content);
        }
    }
}