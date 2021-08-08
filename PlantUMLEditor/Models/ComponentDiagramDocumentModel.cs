﻿using PlantUML;
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
      
        private IEnumerable<string> _autoCompleteItems = Array.Empty<string>();
        private readonly object _locker = new();
        private bool _running = true;
        private static readonly string[] STATICWORDS = new[] { "component", "folder", "cloud", "package" };

        public ComponentDiagramDocumentModel(IConfiguration configuration,
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

                        var z = await PlantUML.UMLComponentDiagramParser.ReadString(text);
                        if (z != null)
                            lock (_locker)
                                _autoCompleteItems = z.Entities.OfType<UMLComponent>()
                                .Select(p => string.IsNullOrEmpty(p.Alias) ? p.Name : p.Alias).Union(
                                    z.Entities.OfType<UMLInterface>().Select(p => p.Name)
                                    )
                               .Union(z.ContainedPackages.Select(z => z.Text))
                                .Union(
                                    z.Entities.OfType<UMLComponent>().Select(p => p.Namespace)
                                    )
                                .Union(STATICWORDS)
                                .ToArray();
                    }
                }
            });
        }

        public override void Close()
        {
            _running = false;
            base.Close();
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

        public override void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {
            base.AutoComplete(autoCompleteParameters);
            base.MatchingAutoCompletes.Clear();

            lock (_locker)
            {
                try
                {
                    if (!string.IsNullOrEmpty(autoCompleteParameters.WordStart) && 
                        !autoCompleteParameters.WordStart.EndsWith("<", StringComparison.InvariantCulture))
                    {
                        foreach (var item in _autoCompleteItems.Where(p => p.StartsWith(autoCompleteParameters.WordStart, StringComparison.InvariantCultureIgnoreCase)))
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

        public override async Task<UMLDiagram?> GetEditedDiagram()
        {
            return await UMLComponentDiagramParser.ReadString(Content);
        }
    }
}