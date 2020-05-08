using PlantUML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class ClassDiagramDocumentModel : DocumentModel
    {
        private readonly Action<UMLClassDiagram, UMLClassDiagram> ChangedCallback = null;
        private IEnumerable<string> _autoCompleteItems = Array.Empty<string>();
        private object _locker = new object();
        private bool _running = true;

        public ClassDiagramDocumentModel(IAutoComplete autoComplete, IConfiguration configuration) : base(autoComplete, configuration)
        {
        }

        public ClassDiagramDocumentModel(Action<UMLClassDiagram, UMLClassDiagram> changedCallback, IAutoComplete autoComplete, IConfiguration configuration)
            : base(autoComplete, configuration)
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

                        var z = await PlantUML.UMLClassDiagramParser.ReadString(text);
                        lock (_locker)
                            _autoCompleteItems = z.DataTypes.Select(p => p.Name);
                    }
                }
            });
        }

        public UMLClassDiagram Diagram { get; set; }

        protected override void RegenDocumentHandler()
        {
            base.RegenDocumentHandler();

            TextEditor.TextClear();

            TextEditor.TextWrite(PlantUMLGenerator.Create(Diagram));
        }

        public override async void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {
            base.AutoComplete(autoCompleteParameters);
            base.MatchingAutoCompletes.Clear();

            lock (_locker)
            {
                if (!string.IsNullOrEmpty(autoCompleteParameters.WordStart))
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
            if (MatchingAutoCompletes.Count > 0)
                base.ShowAutoComplete(autoCompleteParameters.Position);
        }

        public override async Task PrepareSave()
        {
            var z = await PlantUML.UMLClassDiagramParser.ReadString(Content);

            ChangedCallback(Diagram, z);
            Diagram = z;

            await base.PrepareSave();
        }
    }
}