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

        public ClassDiagramDocumentModel(Action< UMLClassDiagram, UMLClassDiagram> changedCallback, IAutoComplete autoComplete, IConfiguration configuration)
            : base(autoComplete, configuration)
        {
            this.ChangedCallback = changedCallback;

        }

        protected override void RegenDocumentHandler()
        {
            base.RegenDocumentHandler();

            this.TextClear();

            this.TextWrite(PlantUMLGenerator.Create(Diagram));

        }

        public UMLClassDiagram Diagram { get; set; }

   
        public override async Task PrepareSave()
        {
            var z = await PlantUML.UMLClassDiagramParser.ReadString(Content);


            ChangedCallback(Diagram, z);
            Diagram = z;

            await base.PrepareSave();
        }

   

        public override async void AutoComplete(Rect rec, string text, int line, string word, int position, int typedLength)
        {
     

            base.AutoComplete(rec, text, line, word, position, typedLength);
            base.MatchingAutoCompletes.Clear();

            var z = await PlantUML.UMLClassDiagramParser.ReadString(this.TextRead());
            _autoCompleteItems = z.DataTypes.Select(p => p.Name);

            if (!string.IsNullOrEmpty(word))
            {
                foreach (var item in _autoCompleteItems.Where(p=>p.StartsWith(word, StringComparison.InvariantCultureIgnoreCase)))
                    base.MatchingAutoCompletes.Add(item);
            }
            else
            {
                foreach (var item in _autoCompleteItems)
                    base.MatchingAutoCompletes.Add(item);
            }
            if(MatchingAutoCompletes.Count > 0) 
                base.ShowAutoComplete(rec);


        }
    }
}