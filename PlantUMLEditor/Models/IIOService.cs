namespace PlantUMLEditor.Models
{
    public interface IIOService
    {
        string GetDirectory();

        string GetSaveFile(string filter, string defaultExt);

        string NewFile(string directory, string fileExtension);
    }
}