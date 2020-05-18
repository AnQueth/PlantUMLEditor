using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace PlantUMLEditor.Models
{
    public interface IAutoComplete
    {
        bool IsPopupVisible { get; }

        void CloseAutoComplete();

        void FocusAutoComplete(System.Windows.Rect rec, IAutoCompleteCallback autoCompleteCallback, bool allowTyping);

        void SendEvent(KeyEventArgs e);
    }
}