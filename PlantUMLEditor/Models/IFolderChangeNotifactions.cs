using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    public interface IFolderChangeNotifactions
    {
        Task Change(string fullPath);
    }
}