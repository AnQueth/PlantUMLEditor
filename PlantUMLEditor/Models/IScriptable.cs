using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    internal interface IScriptable : ITextGetter
    {
        Task<string> ExecuteScript(string script);
    }
}