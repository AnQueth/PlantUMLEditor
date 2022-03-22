using PlantUMLEditor.Controls;

namespace PlantUMLEditor.Models
{
    public interface ITextEditor
    {
        void GotoLine(int lineNumber, string? findText);

        void InsertText(string text);

        void InsertTextAt(string text, int where, int originalLength);

        void ReportError(int line, int character);



        void TextClear();

        string TextRead();

        void TextWrite(string text, bool format, IColorCodingProvider? colorCodingProvider);
        void InsertTextAtCursor(string text);

        void Destroy();
    }
}