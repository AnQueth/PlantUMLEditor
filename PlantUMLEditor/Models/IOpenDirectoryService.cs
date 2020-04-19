namespace PlantUMLEditor.Models
{
    public interface IOpenDirectoryService
    {
        string GetDirectory();

        string NewFile(string directory);
    }
}