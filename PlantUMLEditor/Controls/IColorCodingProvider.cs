using System.Collections.Generic;

namespace PlantUMLEditor.Controls
{
    public interface IColorCodingProvider
    {
        List<FormatResult> FormatText(string text);
    }
}