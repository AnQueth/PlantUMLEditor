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
    internal class ComponentDiagramDocumentModel : TextDocumentModel
    {

        private IEnumerable<string> _autoCompleteItems = Array.Empty<string>();
        private readonly object _locker = new();
        private bool _running = true;
        private static readonly string[] STATICWORDS = new[] { "component", "folder", "cloud", "package" };
        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {
            var imageModel = new PreviewDiagramModel(base._ioService, base._jarLocation, Title);

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

        public ComponentDiagramDocumentModel(IConfiguration configuration,
                 IIOService openDirectoryService,
                 UMLComponentDiagram diagram, string filename, string title, string content) : base(configuration, openDirectoryService,
                      filename, title, content)
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

                         var z = await PlantUML.UMLComponentDiagramParser.ReadString(text);
                         if (z != null)
                         {
                             lock (_locker)
                             {
                                 _autoCompleteItems = z.Entities.OfType<UMLComponent>()
                                 .Select(p => string.IsNullOrEmpty(p.Alias) ? p.Name : p.Alias).Union(
                                     z.Entities.OfType<UMLInterface>().Select(p => p.Name)
                                     )

                                .Union(z.ContainedPackages.Select(p => string.IsNullOrEmpty(p.Alias) ? p.Name : p.Alias))
                                 .Union(
                                     z.Entities.OfType<UMLComponent>().Select(p => p.Namespace)
                                     )
                                 .Union(STATICWORDS)
                                 .ToArray();
                             }
                         }
                     }
                 }
             });
        }

        public override void Close()
        {
            _running = false;
            base.Close();
        }

        public UMLComponentDiagram Diagram
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

        internal void UpdateDiagram(UMLComponentDiagram doc)
        {
            TextEditor?.TextWrite(PlantUMLGenerator.Create(doc), true, GetColorCodingProvider());
        }

        internal override void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {

            base.MatchingAutoCompletes.Clear();

            lock (_locker)
            {
                try
                {
                    if (!string.IsNullOrEmpty(autoCompleteParameters.WordStart) &&
                        !autoCompleteParameters.WordStart.EndsWith("<", StringComparison.InvariantCulture))
                    {
                        foreach (var item in _autoCompleteItems.Where(p => p.StartsWith(autoCompleteParameters.WordStart, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            base.MatchingAutoCompletes.Add(item);
                        }
                    }
                }
                catch { }
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

        public override async Task<UMLDiagram?> GetEditedDiagram()
        {
            return await UMLComponentDiagramParser.ReadString(Content);
        }
    }
}