using PlantUML;
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


        public ClassDiagramDocumentModel(IConfiguration configuration,
            IIOService openDirectoryService) : base(configuration, openDirectoryService)
        {
       
            

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
                        if (z != null)
                            lock (_locker)
                                _autoCompleteItems = z.DataTypes.Select(p => p.Name);
                    }
                }
            });
        }

        public UMLClassDiagram Diagram { get; set; }

        protected override void RegenDocumentHandler()
        {
            string t = PlantUMLGenerator.Create(this.Diagram);

            this.TextEditor.TextWrite(t, true);

            base.RegenDocumentHandler();
        }

        internal void UpdateDiagram(UMLClassDiagram doc)
        {
            if (this.TextEditor == null)
            {
                _bindedAction = () =>
                {
                    this.TextEditor.TextWrite(PlantUMLGenerator.Create(doc), true);
                };
            }
            else
                this.TextEditor.TextWrite(PlantUMLGenerator.Create(doc), true);
        }

        public override   void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {
            base.AutoComplete(autoCompleteParameters);
            base.MatchingAutoCompletes.Clear();

            lock (_locker)
            {
                if (!string.IsNullOrEmpty(autoCompleteParameters.WordStart) 
                    && !autoCompleteParameters.WordStart.EndsWith("<", StringComparison.InvariantCulture))
                {
                    foreach (var item in _autoCompleteItems.Where(p => p.StartsWith(autoCompleteParameters.WordStart, StringComparison.InvariantCultureIgnoreCase)))
                        base.MatchingAutoCompletes.Add(item);
                }
            }
            if (MatchingAutoCompletes.Count > 0)
                base.ShowAutoComplete(autoCompleteParameters.Position);
            else
                base.CloseAutoComplete();
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