using System;
using System.Collections.Generic;
using System.Text;

namespace PlantUMLEditor.Models
{
    public interface ITextEditor
    {
        void GotoLine(int lineNumber);

        void InsertText(string text);

        void InsertTextAt(string text, int where, int originalLength);

        void SetAutoComplete(IAutoComplete autoComplete);

        void TextClear();

        string TextRead();

        void TextWrite(string text);
    }
}