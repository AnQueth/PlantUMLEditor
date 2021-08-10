using System.Windows;


namespace PlantUMLEditor.Models
{
    public class AutoCompleteParameters
    {
        public int Where;
        public int LineNumber;
       
        public int PositionInLine;
        public string Text;
        public int TypedLength;

        public string WordStart;

        public AutoCompleteParameters( string text, int line, string word, int where, int typedLength,   int positionInLine)
        {
            
            Text = text;
            LineNumber = line;
            WordStart = word;
            Where = where;
            TypedLength = typedLength;
         
            PositionInLine = positionInLine;
        }

       
    }
}