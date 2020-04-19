using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    public interface IUMLDocumentCollectionSerialization
    {
        Task<UMLModels.UMLDocumentCollection> Read(string fileName);

        Task Save(UMLModels.UMLDocumentCollection data, string fileName);
    }
}