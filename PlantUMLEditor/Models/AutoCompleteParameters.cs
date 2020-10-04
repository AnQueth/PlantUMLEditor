using System.Windows;
using System.Windows.Input;

namespace PlantUMLEditor.Models
{
    public class AutoCompleteParameters
    {
        public int CaretPosition;
        public int LineNumber;
        public Rect Position;
        public int PositionInLine;
        public string Text;
        public int WordLength;

        public string WordStart;

        public AutoCompleteParameters(Rect rec, string text, int line, string word, int where, int typedLength, System.Windows.Input.Key k, int positionInLine)
        {
            this.Position = rec;
            this.Text = text;
            this.LineNumber = line;
            this.WordStart = word;
            this.CaretPosition = where;
            this.WordLength = typedLength;
            this.Key = k;
            this.PositionInLine = positionInLine;
        }

        public Key Key { get; }
    }
}