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
        private IEnumerable<string> _autoCompleteItems;

        public ClassDiagramDocumentModel(IAutoComplete autoComplete, IConfiguration configuration) : base(autoComplete, configuration)
        {
        }

        public ClassDiagramDocumentModel(Action<UMLClassDiagram, UMLClassDiagram> changedCallback, IAutoComplete autoComplete, IConfiguration configuration)
            : base(autoComplete, configuration)
        {
            this.ChangedCallback = changedCallback;
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

            var z = await PlantUML.UMLClassDiagramParser.ReadString(TextEditor.TextRead());
            _autoCompleteItems = z.DataTypes.Select(p => p.Name);

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