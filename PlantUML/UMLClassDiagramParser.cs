using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUML
{
    public class UMLClassDiagramParser
    {
        public static async Task<UMLClassDiagram> ReadClassDiagramString(string s)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(s)))
            {
                using (StreamReader sr = new StreamReader(ms))
                {
                    UMLClassDiagram c = await ReadClassDiagram(sr, "");

                    return c;
                }
            }
        }

        public static async Task<UMLClassDiagram> ReadClassDiagram(string file)
        {
            using (StreamReader sr = new StreamReader(file))
            {
                UMLClassDiagram c = await ReadClassDiagram(sr, file);

                return c;
            }
        }

        private static async Task<UMLClassDiagram> ReadClassDiagram(StreamReader sr, string fileName)
        {
            UMLClassDiagram d = new UMLClassDiagram(string.Empty, fileName);
            bool started = false;
            string line = null;
            string currentPackage = string.Empty;

            Dictionary<string, UMLDataType> aliases = new Dictionary<string, UMLDataType>();

            while ((line = await sr.ReadLineAsync()) != null)
            {
                line = line.Trim();

                if (line == "@startuml")
                {
                    started = true;
                }

                if (!started)
                    continue;

                UMLDataType DataType = null;

                if (line.StartsWith("title"))
                {
                    d.Title = line.Substring(6);
                    continue;
                }
                else if (line.StartsWith("package"))
                {
                    currentPackage = Clean(line.Substring(8));
                    continue;
                }
                else if (line.StartsWith("class"))
                {
                    if (line.Length > 5)
                        DataType = new UMLClass(currentPackage, Clean(line.Substring(6)));
                }
                else if (line.StartsWith("interface"))
                {
                    if (line.Length > 8)
                        DataType = new UMLInterface(currentPackage, Clean(line.Substring(9)));
                }
                else if (line.Contains(" --"))
                {
                    var items = line.Split(new char[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (items.Length == 2)
                        aliases[items[0]].Base = aliases[items[1]];
                }

                if (DataType != null)
                {
                    d.DataTypes.Add(DataType);
                    while ((line = await sr.ReadLineAsync()) != null)
                    {
                        line = line.Trim();

                        if (line == "}")
                            break;

                        if (line.EndsWith(")"))
                        {
                            var items = line.Split(new char[] { ' ', ',', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

                            UMLDataType c;

                            if (aliases.ContainsKey(items[0]))
                            {
                                c = aliases[items[0]];
                            }
                            else
                            {
                                c = new UMLDataType(items[0], currentPackage);
                                aliases.Add(items[0], c);
                            }

                            List<UMLParameter> pars = new List<UMLParameter>();

                            for (int x = 2; x < items.Length; x += 2)
                            {
                                Tuple<ListTypes, string> p = CreateFrom(items[x]);

                                if (aliases.ContainsKey(p.Item2))
                                {
                                    c = aliases[p.Item2];
                                }
                                else
                                {
                                    c = new UMLDataType(p.Item2, currentPackage);
                                    aliases.Add(p.Item2, c);
                                }

                                pars.Add(new UMLParameter(items[x + 1], c, p.Item1));
                            }

                            DataType.Methods.Add(new UMLMethod(items[1], c, pars.ToArray()));
                        }
                        else
                        {
                            var items = line.Split(new char[] { ' ', ',', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                            if (items.Length < 2)
                                continue;

                            Tuple<ListTypes, string> p = CreateFrom(items[0]);

                            UMLDataType c;

                            if (aliases.ContainsKey(p.Item2))
                            {
                                c = aliases[p.Item2];
                            }
                            else
                            {
                                c = new UMLDataType(p.Item2, currentPackage);
                                aliases.Add(p.Item2, c);
                            }

                            DataType.Properties.Add(new UMLProperty(items[1], c, p.Item1));
                        }

                        if (line == "}")
                            break;
                    }
                }
            }

            return d;
        }

        private static string Clean(string name)
        {
            var t = name.Trim();
            return t.TrimEnd('{').Trim();
        }

        private static Tuple<ListTypes, string> CreateFrom(string v)
        {
            if (v.StartsWith("ireadonlycollection<", StringComparison.InvariantCultureIgnoreCase))
                return new Tuple<ListTypes, string>(ListTypes.IReadOnlyCollection, v.Substring(20).Trim('>'));
            else if (v.StartsWith("list<", StringComparison.InvariantCultureIgnoreCase))
                return new Tuple<ListTypes, string>(ListTypes.List, v.Substring(6).Trim('>'));
            else if (v.EndsWith("[]"))
                return new Tuple<ListTypes, string>(ListTypes.Array, v.Trim().Substring(0, v.Trim().Length - 2));
            else
                return new Tuple<ListTypes, string>(ListTypes.None, v);
        }
    }
}