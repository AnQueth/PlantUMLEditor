using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;

namespace PlantUMLEditor.Models
{
    public static  class GlobalSearch
    {

        public static string? RootDirectory { get; set; }

        public static Task<List<GlobalFindResult>> Find( string text, string[] extensions)
        {
            if (string.IsNullOrEmpty(text))
                return Task.FromResult(new List<GlobalFindResult>());

            ConcurrentBag<GlobalFindResult> res = new();

            // Try to treat the search text as a regex. If it fails to compile, fall back to plain string search.
            Regex? regex = null;
            bool useRegex = false;
            try
            {
                regex = new Regex(text, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                useRegex = true;
            }
            catch (ArgumentException)
            {
                useRegex = false;
            }

            Parallel.ForEach(extensions, (ex) =>
            {
                if (string.IsNullOrEmpty(RootDirectory) || !Directory.Exists(RootDirectory))
                    return;

                foreach (string? file in Directory.GetFiles(RootDirectory, ex, SearchOption.AllDirectories))
                {
                    try
                    {
                        using FileStream? f = File.OpenRead(file);
                        int line = 1;
                        using StreamReader sr = new(f);
                        string? lineText = null;
                        while ((lineText = sr.ReadLine()) != null)
                        {
                            bool isMatch;
                            if (useRegex && regex is not null)
                                isMatch = regex.IsMatch(lineText);
                            else
                                isMatch = lineText.Contains(text, System.StringComparison.InvariantCultureIgnoreCase);

                            if (isMatch)
                            {
                                res.Add(new GlobalFindResult(file, line, lineText, text));

                            }
                            line++;
                        }
                    }
                    catch
                    {
                        // ignore files we can't read
                    }
                }
            });

            return Task.FromResult(res.OrderBy(p => p.FileName).ThenBy(p => p.LineNumber).ToList());
        }
    }
}