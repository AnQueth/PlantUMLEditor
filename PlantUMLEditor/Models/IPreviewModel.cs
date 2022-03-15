namespace PlantUMLEditor.Models
{
    internal interface IPreviewModel
    {
        void Show(string path, string name, bool delete);

        void Stop();
    }
}