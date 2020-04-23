using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal class SequenceDiagramDocumentModel : DocumentModel
    {
        private Action<UMLSequenceDiagram, UMLSequenceDiagram> ChangedCallback;
        private double topAutoComplete;
        private double leftAutoComplete;

        public SequenceDiagramDocumentModel()
        {
            MatchingAutoCompletes = new ObservableCollection<string>();
        }

        public SequenceDiagramDocumentModel(Action<UMLSequenceDiagram, UMLSequenceDiagram> p) : this()
        {
            this.ChangedCallback = p;
        }

        public UMLSequenceDiagram Diagram { get; set; }
        public Dictionary<string, UMLDataType> DataTypes { get; internal set; }

        private object _locker = new object();

        protected override void ContentChanged(  string text)
        {
            PlantUML.UMLSequenceDiagramParser.ReadString(Content, DataTypes, true).ContinueWith(z =>
            {
            
                Diagram = z.Result;
                Diagram.FileName = FileName;
            });
            base.ContentChanged(  text);
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
        }

        public int CurrentCursorIndex
        {
            get { return currentCursorIndex; }
            set  { SetValue(ref  currentCursorIndex , value); }
        }

        public override void AutoComplete(object sender, System.Windows.Input.KeyEventArgs e)
        {
            MatchingAutoComplete = null;
            MatchingAutoCompletes.Clear();

            TextBox d = sender as TextBox;

            CurrentCursorIndex = d.CaretIndex;

           var rec = d.GetRectFromCharacterIndex(d.CaretIndex);

            var line = d.GetLineIndexFromCharacterIndex(d.CaretIndex);
            string text = d.GetLineText(line).Trim();

            UMLSequenceConnection connection = null;

            if (text.StartsWith("participant") || text.StartsWith("actor"))
            {
                if (PlantUML.UMLSequenceDiagramParser.TryParseLifeline(text, this.DataTypes, out var lifeline))
                {
                    lifeline.LineNumber = line;
                    return;
                }
                else
                {
                    foreach (var item in this.DataTypes)
                        this.MatchingAutoCompletes.Add(item.Key);

                    ShowAutoComplete(rec);

                    return;
                }
            }
            else
            {
                if (PlantUML.UMLSequenceDiagramParser.TryParseAllConnections(text, this.Diagram, this.DataTypes, null, out connection))
                {
                    connection.LineNumber = line;
                }
            }

       
      

            if (text.EndsWith(':')
                && connection != null && connection.To != null)
            {
                this.DataTypes[connection.To.DataTypeId].Methods.ForEach(z => this.MatchingAutoCompletes.Add(z.Signature));

                this.DataTypes[connection.To.DataTypeId].Properties.ForEach(z => this.MatchingAutoCompletes.Add(z.Signature));


                ShowAutoComplete(rec);
            }
        }

        private void ShowAutoComplete(Rect rec)
        {
            this.LeftAutoComplete = rec.BottomRight.X;
            this.TopAutoComplete = rec.BottomRight.Y;
            AutoCompleteVisibility = Visibility.Visible;
        }

        public ObservableCollection<string> MatchingAutoCompletes
        {
            get;
        }

        private string _selected;

        public string MatchingAutoComplete
        {
            get
            {
                return _selected;
            }
            set
            {
                SetValue(ref _selected, value);

                if (value != null)
                {
                    string insert = " " + value + " ";
                    if (Content[CurrentCursorIndex + 1] == '\r')
                        CurrentCursorIndex--;

                    Content = Content.Insert(CurrentCursorIndex + 1, insert );

                    CurrentCursorIndex = CurrentCursorIndex + insert.Length;


                    AutoCompleteVisibility = Visibility.Hidden;
                }
            }
        }

        private Visibility _AutoCompleteVisibility;
        private int currentCursorIndex;

        public Visibility AutoCompleteVisibility
        {
            get
            {
                return _AutoCompleteVisibility;
            }
            set
            {
                SetValue(ref _AutoCompleteVisibility, value);
            }
        }

        public double TopAutoComplete
        {
            get { return topAutoComplete; }
            set { SetValue(ref topAutoComplete, value); }
        }

        public double LeftAutoComplete
        {
            get { return leftAutoComplete; }
            set { SetValue(ref leftAutoComplete, value); }
        }
    }
}