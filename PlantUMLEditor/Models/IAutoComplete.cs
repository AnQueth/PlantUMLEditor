using System.Windows.Input;

namespace PlantUMLEditor.Models
{
    public interface IAutoComplete
    {
        bool IsPopupVisible { get; }

        void CloseAutoComplete();

        void ShowAutoComplete(  IAutoCompleteCallback autoCompleteCallback);

       
    }
}