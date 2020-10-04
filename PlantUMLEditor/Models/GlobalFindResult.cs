namespace PlantUMLEditor.Models
{
    public class GlobalFindResult
    {
        public string FileName { get; set; }
        public int LineNumber { get; set; }
        public string Text { get; set; }

        public string SearchText { get; set; }
    }
}