using System;
using System.Collections.Generic;
using System.Text;

namespace PlantUMLEditor.Models
{
    public interface IAutoCompleteCallback
    {
        void Selection(string selection);
        void NewAutoComplete(string text);
    }
}
