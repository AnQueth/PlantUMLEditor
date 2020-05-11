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

        void ReportError(int line, int character);

        void SetAutoComplete(IAutoComplete autoComplete);

        void TextClear();

        string TextRead();

        void TextWrite(string text);
    }
}