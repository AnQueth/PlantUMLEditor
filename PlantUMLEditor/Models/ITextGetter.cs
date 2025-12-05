using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    internal interface ITextGetter
    {
         Task<string> ReadContent();
       
        string FileName { get; }
    }
}