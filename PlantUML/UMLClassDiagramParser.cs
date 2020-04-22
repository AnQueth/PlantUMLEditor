using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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

            Regex baseClass = new Regex("(?<first>\\w+) (?<arrow>[\\-\\.]+) (?<second>\\w+)");

            Regex composition = new Regex("(?<first>\\w+)( | \\\"(?<fm>[01\\*])\\\" )(?<arrow>[\\*o\\!\\<]*[\\-\\.]+[\\*o\\!\\>]*)( | \\\"(?<sm>[01\\*])\\\" )(?<second>\\w+) *:*(?<text>.*)");

            Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))");

            bool swallowingNotes = false;

            while ((line = await sr.ReadLineAsync()) != null)
            {
                line = line.Trim();

                if (line == "@startuml")
                {
                    started = true;
                }

                if (!started)
                    continue;

                if (notes.IsMatch(line))
                {
                    var m = notes.Match(line);
                    if (!m.Groups["sl"].Success)
                    {
                        swallowingNotes = true;
                    }
                }

                if (line.StartsWith("end note"))
                    swallowingNotes = false;

                if (swallowingNotes)
                    continue;


                if (line.StartsWith("participant") || line.StartsWith("actor"))
                    return null;


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
                else if (line.StartsWith("abstract"))
                {
                    if (line.Length > 15)
                        DataType = new UMLClass(currentPackage, true, Clean(line.Substring(15)));
                }
                else if (line.StartsWith("class")  )
                {
                    if (line.Length > 5)
                        DataType = new UMLClass(currentPackage, false, Clean(line.Substring(6)));
                }
                else if (line.StartsWith("interface"))
                {
                    if (line.Length > 8)
                        DataType = new UMLInterface(currentPackage, Clean(line.Substring(9)));
                }
                else if (baseClass.IsMatch(line))
                {

                    var m = baseClass.Match(line);

                    d.DataTypes.Find(p=> p.Name == m.Groups["first"].Value).Base = d.DataTypes.Find(p => p.Name == m.Groups["second"].Value);
                }
                else if (composition.IsMatch(line))
                {

                    var m = composition.Match(line);

                  
                    if(m.Groups["text"].Success)
                    {
                        var propType = d.DataTypes.Find(p => p.Name == m.Groups["second"].Value);
                        var fromType = d.DataTypes.Find(p => p.Name == m.Groups["first"].Value);

                        ListTypes l = ListTypes.None;
                        if (m.Groups["fm"].Success)
                        {

                        }
                        if (m.Groups["sm"].Success)
                        {
                            if(m.Groups["sm"].Value == "*")
                            {
                                l = ListTypes.List;
                            }
                        }



                        fromType.Properties.Add(new UMLProperty(m.Groups["text"].Value, propType, UMLVisibility.Public, ListTypes.None));
                    }
                         
                       
                    
                    
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

                            int index;
                            UMLVisibility visibility;
                            ReadVisibility(items, out index, out visibility);

                            UMLDataType c;

                            if (aliases.ContainsKey(items[index]))
                            {
                                c = aliases[items[index]];
                            }
                            else
                            {
                                c = new UMLDataType(items[index], currentPackage);
                                aliases.Add(items[index], c);
                            }

                            List<UMLParameter> pars = new List<UMLParameter>();

                            for (int x = 2 + index; x < items.Length; x += 2)
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

                            DataType.Methods.Add(new UMLMethod(items[1 + index], c, visibility, pars.ToArray()));
                        }
                        else
                        {
                            var items = line.Split(new char[] { ' ', ',', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                            if (items.Length < 2)
                                continue;
                            int index = 0;
                            UMLVisibility visibility;

                            ReadVisibility(items, out index, out visibility);


                            Tuple<ListTypes, string> p = CreateFrom(items[index]);

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

                            DataType.Properties.Add(new UMLProperty(items[1 + index], c, visibility, p.Item1));
                        }

                        if (line == "}")
                            break;
                    }
                }
            }

            return d;
        }

        private static void ReadVisibility(string[] items, out int index, out UMLVisibility visibility)
        {
            index = 0;
            visibility = UMLVisibility.None;
            if (items[0] == "-")
                visibility = UMLVisibility.Private;
            else if (items[0] == "#")
                visibility = UMLVisibility.Protected;
            else if (items[0] == "+")
                visibility = UMLVisibility.Public;

            if (visibility == UMLVisibility.None)
            {
                visibility = UMLVisibility.Public;

            }
            else
            {
                index++;
            }
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