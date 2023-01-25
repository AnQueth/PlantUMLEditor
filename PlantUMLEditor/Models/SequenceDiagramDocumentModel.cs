using PlantUMLEditor.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class SequenceDiagramDocumentModel : TextDocumentModel
    {
        private string _autoCompleteAppend = string.Empty;
        private bool _dataTypesUpdated = false;

        private static readonly string[] DEFAULTAUTOCOMPLETES = new string[] { "participant", "actor", "database", "queue", "entity" };
        protected override (IPreviewModel? model, Window? window) GetPreviewView()
        {
            PreviewDiagramModel? imageModel = new PreviewDiagramModel(base._ioService, base._jarLocation, Title);

            Preview? previewWindow = new Preview
            {
                DataContext = imageModel
            };

            return (imageModel, previewWindow);

        }
        protected override IColorCodingProvider? GetColorCodingProvider()
        {
            return colorCodingProvider;
        }

        public SequenceDiagramDocumentModel(IConfiguration configuration,
                        IIOService openDirectoryService, UMLSequenceDiagram diagram, LockedList<UMLClassDiagram> dataTypes,
                        string fileName, string title, string content, AutoResetEvent messageCheckerTrigger) :
            base(configuration, openDirectoryService, fileName, title, content, messageCheckerTrigger)
        {
            Diagram = diagram;
            DataTypes = dataTypes;

            colorCodingProvider = new UMLColorCoding();
            indenter = new Indenter();
        }

        protected override IIndenter GetIndenter()
        {
            return indenter;
        }

        public LockedList<UMLClassDiagram> DataTypes
        {
            get; private set;
        }

        private readonly UMLColorCoding colorCodingProvider;
        private readonly Indenter indenter;

        public UMLSequenceDiagram Diagram
        {
            get; private set;
        }

        private void AddAll(UMLDataType uMLDataType, List<string> matchingAutoCompletes, string word)
        {
            uMLDataType.Methods.ForEach(z =>
            {
                if (string.IsNullOrEmpty(word) || z.Signature.Contains(word, StringComparison.InvariantCultureIgnoreCase))
                {
                    MatchingAutoCompletes.Add(z.Signature);
                }
            });
            uMLDataType.Properties.ForEach(z =>
            {
                if (string.IsNullOrEmpty(word) || z.Signature.Contains(word, StringComparison.InvariantCultureIgnoreCase))
                {
                    MatchingAutoCompletes.Add(z.Signature);
                }
            });

            foreach (UMLDataType? item in uMLDataType.Bases)
            {
                AddAll(item, matchingAutoCompletes, word);
            }
        }

        protected override string AppendAutoComplete(string selection)
        {
            if (selection != "participant")
            {
                return selection + _autoCompleteAppend;
            }

            return selection;
        }

        internal void UpdateDiagram(LockedList<UMLClassDiagram> classDocuments)
        {
            DataTypes = classDocuments;
            _dataTypesUpdated = true;
        }

        internal override async void AutoComplete(AutoCompleteParameters autoCompleteParameters)
        {
            _autoCompleteAppend = string.Empty;

            string? text = autoCompleteParameters.LineText.Trim();


            MatchingAutoCompletes.Clear();

            ILookup<string, UMLDataType>? types = DataTypes.SelectMany(p => p.DataTypes).Where(p => p is UMLClass || p is UMLInterface).ToLookup(p => p.Name);
            if (text.StartsWith("participant", StringComparison.InvariantCulture)
                || text.StartsWith("actor", StringComparison.InvariantCulture))
            {
                if (PlantUML.UMLSequenceDiagramParser.TryParseLifeline(text, types, autoCompleteParameters.LineNumber, out UMLSequenceLifeline? lifeline) && lifeline.DataTypeId != null)
                {

                    return;
                }
                else
                {
                    foreach (IGrouping<string, UMLDataType>? item in types)
                    {
                        if (string.IsNullOrEmpty(autoCompleteParameters.TypedWord) || item.Key.Contains(autoCompleteParameters.TypedWord, StringComparison.InvariantCultureIgnoreCase))
                        {
                            MatchingAutoCompletes.Add(item.Key);
                        }
                    }

                    if (MatchingAutoCompletes.Count > 0)
                    {
                        ShowAutoComplete();
                    }

                    return;
                }
            }

            string? str = TextEditor?.TextRead();
            if (str is not null)
            {
                UMLSequenceDiagram? diagram = await PlantUML.UMLSequenceDiagramParser.ReadString(str, DataTypes, true);

                if (diagram != null)
                {


                    if (PlantUML.UMLSequenceDiagramParser.TryParseAllConnections(text, diagram, types, null, 0,
                        out UMLSequenceConnection? connection))
                    {
                        if (text.Length - 2 > autoCompleteParameters.PositionInLine
                            && autoCompleteParameters.PositionInLine < text.IndexOf(":", StringComparison.InvariantCulture))
                        {
                            return;
                        }

                        if (connection.To != null && connection.To.DataTypeId != null)
                        {
                            foreach (UMLDataType? t in types[connection.To.DataTypeId])
                            {
                                AddAll(t, MatchingAutoCompletes, autoCompleteParameters.TypedWord);


                            }
                            if (MatchingAutoCompletes.Count > 0)
                            {
                                ShowAutoComplete();
                            }

                            return;
                        }
                    }

                    if (text.EndsWith("return", StringComparison.InvariantCulture))
                    {
                        foreach (string? item in diagram.LifeLines.Where(p => string.IsNullOrEmpty(autoCompleteParameters.TypedWord) || p.Text.StartsWith(autoCompleteParameters.TypedWord, StringComparison.InvariantCultureIgnoreCase)).Select(p => p.Text))
                        {
                            MatchingAutoCompletes.Add(item);
                        }

                        if (MatchingAutoCompletes.Count > 0)
                        {
                            ShowAutoComplete();
                        }

                        return;
                    }

                    foreach (string? item in diagram.LifeLines.Where(p => string.IsNullOrEmpty(autoCompleteParameters.TypedWord) || p.Alias.StartsWith(autoCompleteParameters.TypedWord, StringComparison.InvariantCultureIgnoreCase)).Select(p => p.Alias))
                    {
                        MatchingAutoCompletes.Add(item);
                    }
                }
            }
            if (!MatchingAutoCompletes.Any())
            {
                foreach (string? item in DEFAULTAUTOCOMPLETES.Where(p => string.IsNullOrEmpty(autoCompleteParameters.TypedWord) || p.StartsWith(autoCompleteParameters.TypedWord, StringComparison.InvariantCultureIgnoreCase)))
                {
                    MatchingAutoCompletes.Add(item);
                }
            }



            if (MatchingAutoCompletes.Count > 0)
            {
                ShowAutoComplete();
            }
        }


        private UMLDiagram? _diagram;

        public override async Task<UMLDiagram?> GetEditedDiagram()
        {
            if (DocGeneratorDirty || _diagram is null || _dataTypesUpdated)
            {


                if (Content is null)
                {
                    return null;
                }

                _diagram = await PlantUML.UMLSequenceDiagramParser.ReadString(Content, DataTypes, false);

                if (_dataTypesUpdated)
                {
                    _dataTypesUpdated = false;
                }
                DocGeneratorDirty = false;
            }
            return _diagram;



        }



    }
}