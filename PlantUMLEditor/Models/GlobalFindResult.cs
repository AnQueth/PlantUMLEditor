namespace PlantUMLEditor.Models
{
    public record GlobalFindResult
    (
         string FileName,
         int LineNumber,
         string Text ,

         string SearchText
    );
}