using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace PlantUMLEditor.Models
{
    public class AutoCompleteParameters
    {
        public int CaretPosition;
        public int LineNumber;
        public Rect Position;
        public string Text;
        public int WordLength;
        public string WordStart;

        public AutoCompleteParameters(Rect rec, string text, int line, string word, int where, int typedLength)
        {
            this.Position = rec;
            this.Text = text;
            this.LineNumber = line;
            this.WordStart = word;
            this.CaretPosition = where;
            this.WordLength = typedLength;
        }
    }
}