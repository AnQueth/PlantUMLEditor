using Newtonsoft.Json;
using PlantUMLEditor.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUMLEditor.Services
{
    class UMLDocumentCollectionSerialization : IUMLDocumentCollectionSerialization
    {
        private JsonSerializerSettings GetOptions()
        {
            return new JsonSerializerSettings()
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
                 TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full,
                 TypeNameHandling = TypeNameHandling.Auto
                  



            };
        }

        public async Task<UMLDocumentCollection> Read(string fileName)
        {

            if (File.Exists(fileName))
            {

                string s = File.ReadAllText(fileName);

                    return  JsonConvert.DeserializeObject<UMLModels.UMLDocumentCollection>(s, GetOptions());

                
            }
            return new UMLDocumentCollection();
        }

        public async Task Save(UMLDocumentCollection data, string fileName)
        {
             

                 string r = JsonConvert.SerializeObject( data, GetOptions());
                await File.WriteAllTextAsync(fileName ,r);

            
        }
    }
}
