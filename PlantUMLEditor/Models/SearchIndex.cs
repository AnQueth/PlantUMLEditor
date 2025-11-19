using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace PlantUMLEditor.Models
{
    internal class SearchIndex(string folder)
    {

        public static IReadOnlyCollection<string> SupportedExtensions { get; } = new[]
      {
            ".class.puml",
            ".component.puml",
            ".seq.puml",
            ".json.puml",
            // generic PlantUML files
            ".puml",
            // markdown and yaml
            ".md",
            ".yml",
            // images used by markdown/image previews
            ".png",
            ".jpg"
        };

        public static bool IsSupported(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            // ensure longer/multi-part extensions are checked first
            foreach (var ext in SupportedExtensions.OrderByDescending(s => s.Length))
            {
                if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }


        public async Task Initialize()
        {


            List<string> files = new();

            foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                if (IsSupported(file))
                {

                    await foreach (var chunk in Chunker.CreateChunksAsync(new[] { file }))
                    {
                        
                    }

                }
            }

        }
    }
}
