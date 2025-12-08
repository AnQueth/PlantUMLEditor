namespace PlantUMLEditor.Models
{
    public interface IIOService
    {
        string? GetDirectory();
        string? GetFile(params string[] extensions);
        string? GetSaveFile(string filter, string defaultExt);

        string? NewFile(string directory, string fileExtension);
    }
}