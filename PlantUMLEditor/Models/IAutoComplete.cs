using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace PlantUMLEditor.Models
{
   public  interface IAutoComplete
    {
        void FocusAutoComplete(System.Windows.Rect rec, IAutoCompleteCallback autoCompleteCallback, bool allowTyping);

        void CloseAutoComplete();
    }
}
