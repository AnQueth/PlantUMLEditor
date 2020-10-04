namespace PlantUMLEditor.Models
{
    public interface IAutoCompleteCallback
    {
        void NewAutoComplete(string text);

        void Selection(string selection);
    }
}