using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUML
{
    public class UnknownDiagramParser
    {
       

           private static Regex _title = new Regex("^title (?<title>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        

        private static async Task<UMLUnknownDiagram> Read(StreamReader sr, string fileName)
        {
            UMLUnknownDiagram d = new UMLUnknownDiagram(string.Empty, fileName);
            bool started = false;
            string line = null;

            

            int linenumber = 0;

            while ((line = await sr.ReadLineAsync()) != null)
            {
                linenumber++;
                line = line.Trim();

                if (line == "@startuml")
                {
                    started = true;
                }

                if (!started)
                    continue;

              
             

               

                if (line.StartsWith("title"))
                {
                    if (line.Length > 6)
                        d.Title = line.Substring(6);
                    continue;
                }
               
            }

            return d;
        }

        public static async Task<UMLUnknownDiagram> ReadFile(string file)
        {
            using (StreamReader sr = new StreamReader(file))
            {
                UMLUnknownDiagram c = await Read(sr, file);

                return c;
            }
        }

        public static async Task<UMLUnknownDiagram> ReadString(string s)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(s)))
            {
                using (StreamReader sr = new StreamReader(ms))
                {
                    UMLUnknownDiagram c = await Read(sr, "");

                    return c;
                }
            }
        }
    }
}