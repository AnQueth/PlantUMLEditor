namespace PlantUMLEditor.Models
{
    public class DocumentMessage
    {
        public string FileName { get; set; }

        public bool Warning { get; set; }

        public string Text { get; set; }

        public int LineNumber { get; set; }
    }
}