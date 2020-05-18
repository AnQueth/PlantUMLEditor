namespace PlantUMLEditor.Controls
{
    public class FindResult
    {
        private int index;
        private string line;

        public FindResult(int index, string line, int lineNumber, string replacePreview)
        {
            this.Index = index;
            this.Line = line;
            LineNumber = lineNumber;
            ReplacePreview = replacePreview;
        }

        public int Index { get => index; set => index = value; }
        public string Line { get => line; set => line = value; }
        public int LineNumber { get; private set; }
        public string ReplacePreview { get; private set; }
    }
}