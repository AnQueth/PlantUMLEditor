using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    public   class GlobalSearch
    {
        public static Task<List<GlobalFindResult>> Find(string rootDirectory, string text, string[] extensions)
        {
            ConcurrentBag<GlobalFindResult> res = new();
            Parallel.ForEach(extensions, (ex) =>
            {

                foreach (string? file in Directory.GetFiles(rootDirectory, ex, SearchOption.AllDirectories))
                {
                    using FileStream? f = File.OpenRead(file);
                    int line = 1;
                    using StreamReader sr = new(f);
                    string? lineText = null;
                    while ((lineText = sr.ReadLine()) != null)
                    {
                        if (lineText.Contains(text, System.StringComparison.InvariantCultureIgnoreCase))
                        {
                            res.Add(new GlobalFindResult(file, line, lineText, text));

                        }
                        line++;
                    }
                }
            });

            return Task.FromResult(res.OrderBy(p => p.FileName).ThenBy(p => p.LineNumber).ToList());
        }
    }
}