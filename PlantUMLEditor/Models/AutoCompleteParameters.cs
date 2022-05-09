namespace PlantUMLEditor.Models
{
    public record AutoCompleteParameters(string LineText, int LineNumber,
        string TypedWord, int IndexInText, int TypedLength, int PositionInLine)
    {



    }
}