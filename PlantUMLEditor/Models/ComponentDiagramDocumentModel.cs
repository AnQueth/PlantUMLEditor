using PlantUML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class ComponentDiagramDocumentModel : DocumentModel
    {
        private readonly Action<UMLComponentDiagram, UMLComponentDiagram> ChangedCallback = null;
        private IEnumerable<string> _autoCompleteItems = Array.Empty<string>();
        private object _locker = new object();
        private bool _running = true;

        public ComponentDiagramDocumentModel(IConfiguration configuration) : base(configuration)
        {
        }

        public ComponentDiagramDocumentModel(Action<UMLComponentDiagram, UMLComponentDiagram> changedCallback, IConfiguration configuration)
            : base(configuration)
        {
            this.ChangedCallback = changedCallback;

            Task.Run(async () =>
            {
                while (_running)
                {
                    await Task.Delay(1000);

                    if (this.IsDirty)
                    {
                        string text = string.Empty;
                        text = (string)Application.Current.Dispatcher.Invoke((() =>
                        {
                            return this.TextEditor.TextRead();
                        }));

                        var z = await PlantUML.UMLComponentDiagramParser.ReadString(text);
                        if (z != null)
                            lock (_locker)
                                _autoCompleteItems = z.Entities.Cast<UMLComponent>()
                                .Select(p => string.IsNullOrEmpty(p.Alias) ? p.Name : p.Alias).Union(
                                    z.Entities.Cast<UMLInterface>().Select(p => p.Name)
                                    ).ToList();
                    }
                }
            });
        }

        public UMLComponentDiagram Diagram { get; set; }

        protected override void RegenDocumentHandler()
        {
            string t = PlantUMLGenerator.Create(this.Diagram);

            this.TextEditor.TextWrite(t, true);

            base.RegenDocumentHandler();
        }

        internal void UpdateDiagram(UMLComponentDiagram doc)
        {
            this.TextEditor.TextWrite(PlantUMLGenerator.Create(doc), true);
        }

        public override async void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {
            base.AutoComplete(autoCompleteParameters);
            base.MatchingAutoCompletes.Clear();

            lock (_locker)
            {try
                {
                    if (!string.IsNullOrEmpty(autoCompleteParameters.WordStart) && !autoCompleteParameters.WordStart.EndsWith("<"))
                    {
                        foreach (var item in _autoCompleteItems.Where(p => p.StartsWith(autoCompleteParameters.WordStart, StringComparison.InvariantCultureIgnoreCase)))
                            base.MatchingAutoCompletes.Add(item);
                    }
                    else
                    {
                        foreach (var item in _autoCompleteItems)
                            base.MatchingAutoCompletes.Add(item);
                    }
                }
                catch { }
            }
            if (MatchingAutoCompletes.Count > 0)
                base.ShowAutoComplete(autoCompleteParameters.Position);
            else
                base.CloseAutoComplete();
        }

        public override async Task<UMLDiagram> GetEditedDiagram()
        {
            return await UMLComponentDiagramParser.ReadString(Content);
        }
    }
}