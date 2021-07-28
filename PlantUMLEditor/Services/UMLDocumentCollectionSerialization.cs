using Newtonsoft.Json;
using PlantUMLEditor.Models;
using System.IO;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUMLEditor.Services
{
    internal class UMLDocumentCollectionSerialization : IUMLDocumentCollectionSerialization
    {
        private static JsonSerializerSettings GetOptions()
        {
            return new JsonSerializerSettings()
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
          
                TypeNameHandling = TypeNameHandling.Auto
            };
        }

        public async Task<UMLDocumentCollection> Read(string fileName)
        {
            if (File.Exists(fileName))
            {
               
                string s = await File.ReadAllTextAsync(fileName);

                return JsonConvert.DeserializeObject<UMLModels.UMLDocumentCollection>(s, GetOptions());
            }
            return new UMLDocumentCollection();
        }

        public async Task Save(UMLDocumentCollection data, string fileName)
        {
            string r = JsonConvert.SerializeObject(data, GetOptions());
            await File.WriteAllTextAsync(fileName, r);
        }
    }
}