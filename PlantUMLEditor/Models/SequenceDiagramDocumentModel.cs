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
        private Action<UMLSequenceDiagram, UMLSequenceDiagram> ChangedCallback;


        public SequenceDiagramDocumentModel(IAutoComplete autoComplete, IConfiguration configuration) : base(autoComplete, configuration)
        {

        }

        public SequenceDiagramDocumentModel(Action<UMLSequenceDiagram, UMLSequenceDiagram> p, IAutoComplete autoComplete, IConfiguration configuration)
            : this(autoComplete, configuration)
        {
            this.ChangedCallback = p;
        }

        public UMLSequenceDiagram Diagram { get; set; }
        public List<UMLClassDiagram> DataTypes { get; internal set; }

        private object _locker = new object();
        private UMLDataType _currentAutoCompleteLifeLineType;



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




        public override async void AutoComplete(Rect rec, string text, int line, string word, int position, int typedLength)
        {
            text = text.Trim();
            _currentAutoCompleteLifeLineType = null;
            base.AutoComplete(rec, text, line, word, position, typedLength);

            MatchingAutoCompletes.Clear();


            var types = this.DataTypes.SelectMany(p => p.DataTypes).ToDictionary(p => p.Name);

            UMLSequenceConnection connection = null;

            if (text.StartsWith("participant") || text.StartsWith("actor"))
            {


                if (PlantUML.UMLSequenceDiagramParser.TryParseLifeline(text, types, out var lifeline))
                {
                    lifeline.LineNumber = line;
                    return;
                }
                else
                {
                    foreach (var item in types)
                        if (string.IsNullOrEmpty(word) || item.Key.StartsWith(word, StringComparison.InvariantCultureIgnoreCase))
                            this.MatchingAutoCompletes.Add(item.Key);

                    if (this.MatchingAutoCompletes.Count > 0)
                        ShowAutoComplete(rec, false);

                    return;
                }
            }



            var diagram = await PlantUML.UMLSequenceDiagramParser.ReadString(this.TextRead(), DataTypes, true);



            if (text.EndsWith(':') && PlantUML.UMLSequenceDiagramParser.TryParseAllConnections(text, diagram, types, null, out connection))
            {
                if (connection.To != null && connection.To.DataTypeId != null)
                {
                    AddAll(types[connection.To.DataTypeId], this.MatchingAutoCompletes, word);


                    _currentAutoCompleteLifeLineType = types[connection.To.DataTypeId];

                    if (this.MatchingAutoCompletes.Count > 0)
                        ShowAutoComplete(rec, true);

                    return;
                }
            }

            if (text.EndsWith("return"))
            {
                foreach (var item in diagram.LifeLines.Where(p => string.IsNullOrEmpty(word) || p.Text.StartsWith(word, StringComparison.InvariantCultureIgnoreCase)).Select(p => p.Text))
                    this.MatchingAutoCompletes.Add(item);

                if (this.MatchingAutoCompletes.Count > 0)
                    ShowAutoComplete(rec, false);
                
                return;
            }



            foreach (var item in diagram.LifeLines.Where(p => string.IsNullOrEmpty(word) || p.Alias.StartsWith(word, StringComparison.InvariantCultureIgnoreCase)).Select(p => p.Alias))
                this.MatchingAutoCompletes.Add(item);

            if (this.MatchingAutoCompletes.Count > 0)
                ShowAutoComplete(rec, false);

        }

        public override void NewAutoComplete(string text)
        {
            base.NewAutoComplete(text);


            //UMLClassDiagramParser.TryParseLineForDataType(text, new Dictionary<string, UMLDataType>(), _currentAutoCompleteLifeLineType);

            base.InsertTextAt(text, base.LastIndex, base.LastWordLength);

        }

        private void AddAll(UMLDataType uMLDataType, ObservableCollection<string> matchingAutoCompletes, string word)
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


    }
}