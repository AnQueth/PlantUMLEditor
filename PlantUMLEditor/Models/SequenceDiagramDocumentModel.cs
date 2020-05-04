using PlantUML;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration.Internal;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class SequenceDiagramDocumentModel : DocumentModel
    {
        private string _autoCompleteAppend = string.Empty;
        private UMLDataType _currentAutoCompleteLifeLineType;
        private object _locker = new object();
        private Action<UMLSequenceDiagram, UMLSequenceDiagram> ChangedCallback;

        public SequenceDiagramDocumentModel(IAutoComplete autoComplete, IConfiguration configuration) : base(autoComplete, configuration)
        {
        }

        public SequenceDiagramDocumentModel(Action<UMLSequenceDiagram, UMLSequenceDiagram> p, IAutoComplete autoComplete, IConfiguration configuration)
            : this(autoComplete, configuration)
        {
            this.ChangedCallback = p;
        }

        public List<UMLClassDiagram> DataTypes { get; internal set; }
        public UMLSequenceDiagram Diagram { get; set; }

        private void AddAll(UMLDataType uMLDataType, List<string> matchingAutoCompletes, string word)
        {
            uMLDataType.Methods.ForEach(z =>
            {
                if (string.IsNullOrEmpty(word) || z.Signature.StartsWith(word, StringComparison.InvariantCultureIgnoreCase))
                    this.MatchingAutoCompletes.Add(z.Signature);
            });
            uMLDataType.Properties.ForEach(z =>
            {
                if (string.IsNullOrEmpty(word) || z.Signature.StartsWith(word, StringComparison.InvariantCultureIgnoreCase))
                    this.MatchingAutoCompletes.Add(z.Signature);
            });

            if (uMLDataType.Base != null)
                AddAll(uMLDataType.Base, matchingAutoCompletes, word);
        }

        protected override string AppendAutoComplete(string selection)
        {
            return selection + _autoCompleteAppend;
        }

        public override async void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {
            _autoCompleteAppend = string.Empty;

            var text = autoCompleteParameters.Text.Trim();
            _currentAutoCompleteLifeLineType = null;
            base.AutoComplete(autoCompleteParameters);

            MatchingAutoCompletes.Clear();

            var types = this.DataTypes.SelectMany(p => p.DataTypes).ToDictionary(p => p.Name);

            UMLSequenceConnection connection = null;

            if (text.StartsWith("participant") || text.StartsWith("actor"))
            {
                if (PlantUML.UMLSequenceDiagramParser.TryParseLifeline(text, types, out var lifeline))
                {
                    lifeline.LineNumber = autoCompleteParameters.LineNumber;
                    return;
                }
                else
                {
                    foreach (var item in types)
                        if (string.IsNullOrEmpty(autoCompleteParameters.WordStart) || item.Key.StartsWith(autoCompleteParameters.WordStart, StringComparison.InvariantCultureIgnoreCase))
                            this.MatchingAutoCompletes.Add(item.Key);

                    if (this.MatchingAutoCompletes.Count > 0)
                        ShowAutoComplete(autoCompleteParameters.Position, false);

                    return;
                }
            }

            var diagram = await PlantUML.UMLSequenceDiagramParser.ReadString(TextEditor.TextRead(), DataTypes, true);

            if (diagram == null)
                return;

            if (text.EndsWith(':') && PlantUML.UMLSequenceDiagramParser.TryParseAllConnections(text, diagram, types, null, out connection))
            {
                if (connection.To != null && connection.To.DataTypeId != null)
                {
                    AddAll(types[connection.To.DataTypeId], this.MatchingAutoCompletes, autoCompleteParameters.WordStart);

                    _currentAutoCompleteLifeLineType = types[connection.To.DataTypeId];

                    if (this.MatchingAutoCompletes.Count > 0)
                        ShowAutoComplete(autoCompleteParameters.Position, true);

                    return;
                }
            }

            if (text.EndsWith("return"))
            {
                foreach (var item in diagram.LifeLines.Where(p => string.IsNullOrEmpty(autoCompleteParameters.WordStart) || p.Text.StartsWith(autoCompleteParameters.WordStart, StringComparison.InvariantCultureIgnoreCase)).Select(p => p.Text))
                    this.MatchingAutoCompletes.Add(item);

                if (this.MatchingAutoCompletes.Count > 0)
                    ShowAutoComplete(autoCompleteParameters.Position, false);

                return;
            }

            foreach (var item in diagram.LifeLines.Where(p => string.IsNullOrEmpty(autoCompleteParameters.WordStart) || p.Alias.StartsWith(autoCompleteParameters.WordStart, StringComparison.InvariantCultureIgnoreCase)).Select(p => p.Alias))
                this.MatchingAutoCompletes.Add(item);

            if (!autoCompleteParameters.Text.Contains("-"))
                _autoCompleteAppend = " -> ";
            else
                _autoCompleteAppend = " : ";

            this.MatchingAutoCompletes.Add("participant");

            if (this.MatchingAutoCompletes.Count > 0)
                ShowAutoComplete(autoCompleteParameters.Position, false);
        }

        public override void NewAutoComplete(string text)
        {
            base.NewAutoComplete(text);

            //UMLClassDiagramParser.TryParseLineForDataType(text, new Dictionary<string, UMLDataType>(), _currentAutoCompleteLifeLineType);

            TextEditor.InsertTextAt(text, base.AutoCompleteParameters.CaretPosition, base.AutoCompleteParameters.WordLength);
        }

        public override async Task PrepareSave()
        {
            var z = await PlantUML.UMLSequenceDiagramParser.ReadString(Content, DataTypes, false);

            lock (_locker)
            {
                z.FileName = FileName;

                ChangedCallback(Diagram, z);
                Diagram = z;
            }

            await base.PrepareSave();
        }
    }
}