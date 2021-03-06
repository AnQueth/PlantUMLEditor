﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    public class GlobalSearch
    {
        public Task<List<GlobalFindResult>> Find(string rootDirectory, string text, string[] extensions)
        {
            ConcurrentBag<GlobalFindResult> res = new ConcurrentBag<GlobalFindResult>();
            Parallel.ForEach(extensions, (ex) =>
            {
                foreach (var file in Directory.GetFiles(rootDirectory, ex, SearchOption.AllDirectories))
                {
                    using (var f = File.OpenRead(file))
                    {
                        int line = 1;
                        using (StreamReader sr = new StreamReader(f))
                        {
                            string lineText = null;
                            while ((lineText = sr.ReadLine()) != null)
                            {
                                if (lineText.ToLowerInvariant().Contains(text.ToLowerInvariant()))
                                {
                                    res.Add(new GlobalFindResult()
                                    {
                                        FileName = file,
                                        LineNumber = line,
                                        Text = lineText,
                                        SearchText = text
                                    });
                                }
                                line++;
                            }
                        }
                    }
                }
            });

            return Task.FromResult(res.OrderBy(p=>p.FileName).ThenBy(p=>p.LineNumber).ToList());
        }
    }
}