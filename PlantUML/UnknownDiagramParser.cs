using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUML
{
    public class UnknownDiagramParser
    {


        private static readonly Regex _title = new("^title (?<title>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);




        private static async Task<UMLUnknownDiagram> Read(StreamReader sr, string fileName)
        {
            UMLUnknownDiagram d = new(string.Empty, fileName);
            bool started = false;
            string? line = null;



            int linenumber = 0;

            while ((line = await sr.ReadLineAsync()) != null)
            {
                linenumber++;
                line = line.Trim();

                if (line is "@startuml" or "@startmindmap")
                {
                    started = true;
                }

                if (!started)
                {
                    continue;
                }

                if (line.StartsWith("title", StringComparison.InvariantCulture))
                {
                    if (line.Length > 6)
                    {
                        d.Title = line[6..];
                    }

                    continue;
                }

            }

            return d;
        }

        public static async Task<UMLUnknownDiagram> ReadFile(string file)
        {
            using StreamReader sr = new(file);
            UMLUnknownDiagram c = await Read(sr, file);
            if (string.IsNullOrWhiteSpace(c.Title))
            {
                c.Title = Path.GetFileName(file);
            }

            return c;
        }

        public static async Task<UMLUnknownDiagram> ReadString(string s)
        {
            using MemoryStream ms = new(Encoding.UTF8.GetBytes(s));
            using StreamReader sr = new(ms);
            UMLUnknownDiagram c = await Read(sr, "");

            return c;
        }
    }
}