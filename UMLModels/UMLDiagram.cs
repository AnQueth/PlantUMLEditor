namespace UMLModels
{
    public  abstract class UMLDiagram
    {
        public UMLDiagram(string title, string fileName)
        {
            Title = title;
            FileName = fileName;
        }
        public string FileName { get; set; }

        public string Title { get; set; }
    }
}