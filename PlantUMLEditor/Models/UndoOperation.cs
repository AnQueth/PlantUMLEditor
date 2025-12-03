using static PlantUMLEditor.Models.MainModel;

namespace PlantUMLEditor.Models
{
    public enum UndoTypes
    {
        Replace,
        Positional,
        ReplaceAll
    }
    public record UndoOperation(UndoTypes undoType, string fileName, string textBefore, string textAfter);
}