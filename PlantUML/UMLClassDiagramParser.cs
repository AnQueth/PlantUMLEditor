using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Markup;
using UMLModels;

namespace PlantUML
{
    public class UMLClassDiagramParser : IPlantUMLParser
    {
        private const string PACKAGE = "package";

        private static Regex _class = new Regex("(?<abstract>abstract)*\\s*class\\s+(?<name>[\\w\\<\\>]+)\\s+{", RegexOptions.Compiled);

        private static Regex _classLine = new Regex("((?<b>[\\{])(?<modifier>\\w+)*(?<-b>[\\}]))*\\s*(?<visibility>[\\+\\-\\#\\~]*)\\s*((?<type>[\\w\\<\\>\\[\\]\\,]+)\\s)*\\s*(?<name>[\\w\\.\\<\\>]+)\\(\\s*(?<params>.*)\\)", RegexOptions.Compiled);

        private static Regex _packageRegex = new Regex("(package|together) \\\"*(?<package>[\\w\\s\\.]+)\\\"* *\\{", RegexOptions.Compiled);

        private static Regex _propertyLine = new Regex("^\\s*(?<visibility>[\\+\\-\\~\\#])*\\s*(?<type>[\\w\\<\\>\\,\\[\\] ]+)\\s+(?<name>[\\w_]+)\\s*$", RegexOptions.Compiled);

        private static Regex baseClass = new Regex("(?<first>\\w+)(\\<((?<generics1>[\\s\\w]+)\\,*)*\\>)*\\s+(?<arrow>[\\-\\.]+)\\s+(?<second>[\\w]+)(\\<((?<generics2>[\\s\\w]+)\\,*)*\\>)*", RegexOptions.Compiled);

        private static Regex composition = new Regex("(?<first>\\w+)( | \\\"(?<fm>[01\\*])\\\" )(?<arrow>[\\*o\\!\\<]*[\\-\\.]+[\\*o\\!\\>]*)( | \\\"(?<sm>[01\\*])\\\" )(?<second>\\w+) *:*(?<text>.*)", RegexOptions.Compiled);

        private static Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))", RegexOptions.Compiled);

        private static string Clean(string name)
        {
            var t = name.Trim();
            return t.TrimEnd('{').Trim();
        }

        private static Tuple<ListTypes, string> CreateFrom(string v)
        {
            if (v.StartsWith("ireadonlycollection<", StringComparison.OrdinalIgnoreCase))
                return new Tuple<ListTypes, string>(ListTypes.IReadOnlyCollection, v.Substring(20).Trim('>', ' '));
            else if (v.StartsWith("list<", StringComparison.OrdinalIgnoreCase))
                return new Tuple<ListTypes, string>(ListTypes.List, v.Substring(6).Trim('>', ' '));
            else if (v.EndsWith("[]"))
                return new Tuple<ListTypes, string>(ListTypes.Array, v.Trim().Substring(0, v.Trim().Length - 2));
            else
                return new Tuple<ListTypes, string>(ListTypes.None, v);
        }

        private static string GetPackage(Stack<string> packages)
        {
            StringBuilder sb = new StringBuilder();
            int x = 0;
            foreach (var item in packages.Reverse())
            {
                sb.Append(item);
                if (x < packages.Count - 1)
                    sb.Append(".");
                x++;
            }

            return sb.ToString();
        }

        private static async Task<UMLClassDiagram> ReadClassDiagram(StreamReader sr, string fileName)
        {
            UMLClassDiagram d = new UMLClassDiagram(string.Empty, fileName);
            bool started = false;
            string line = null;

            Stack<string> packages = new Stack<string>();

            Dictionary<string, UMLDataType> aliases = new Dictionary<string, UMLDataType>();

            bool swallowingNotes = false;

            Stack<string> brackets = new Stack<string>();
            Regex removeGenerics = new Regex("\\w+");
            Stack<UMLPackage> packagesStack = new Stack<UMLPackage>();

            UMLPackage defaultPackage = new UMLPackage("");
            d.Package = defaultPackage;
            packagesStack.Push(defaultPackage);
            var currentPackage = defaultPackage;

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
                    else
                        swallowingNotes = false;

                    currentPackage.Children.Add(new UMLNote(line));
                    continue;
                }

                if (line.StartsWith("end note"))
                {
                    if (currentPackage.Children.Last() is UMLNote n)
                    {
                        n.Text += "\r\nend note";
                    }
                    swallowingNotes = false;
                }
                if (swallowingNotes)
                {
                    if (currentPackage.Children.Last() is UMLNote n)
                    {
                        n.Text += "\r\n" + line;
                    }
                    continue;
                }
                if (line.StartsWith("participant") || line.StartsWith("actor"))
                    return null;

                UMLDataType DataType = null;

                if (line == "}" && brackets.Count > 0 && brackets.Peek() == PACKAGE)
                {
                    brackets.Pop();
                    packages.Pop();
                    packagesStack.Pop();
                    currentPackage = packagesStack.First();
                }

                if (line.StartsWith("title"))
                {
                    if (line.Length > 6)
                        d.Title = line.Substring(6);
                    continue;
                }
                else if (_packageRegex.IsMatch(line))
                {
                    var s = _packageRegex.Match(line);

                    packages.Push(Clean(s.Groups[PACKAGE].Value));
                    brackets.Push(PACKAGE);

                    var c = new UMLPackage(Clean(s.Groups[PACKAGE].Value));
                    currentPackage.Children.Add(c);
                    currentPackage = c;

                    packagesStack.Push(c);

                    continue;
                }
                else if (_class.IsMatch(line))
                {
                    var g = _class.Match(line);
                    if (string.IsNullOrEmpty(g.Groups["name"].Value))
                        continue;

                    string package = GetPackage(packages);

                    DataType = new UMLClass(package, !string.IsNullOrEmpty(g.Groups["abstract"].Value)
                        , Clean(g.Groups["name"].Value));

                    if (line.EndsWith("{"))
                    {
                        brackets.Push("class");
                    }
                }
                else if (line.StartsWith("enum"))
                {
                    string package = GetPackage(packages);
                    if (line.Length > 4)
                        DataType = new UMLEnum(package, Clean(line.Substring(5)));

                    if (line.EndsWith("{"))
                    {
                        brackets.Push("interface");
                    }
                }
                else if (line.StartsWith("interface"))
                {
                    string package = GetPackage(packages);
                    if (line.Length > 8)
                        DataType = new UMLInterface(package, Clean(line.Substring(9)));

                    if (line.EndsWith("{"))
                    {
                        brackets.Push("interface");
                    }
                }
                else if (baseClass.IsMatch(line))
                {
                    var m = baseClass.Match(line);

                    d.DataTypes.Find(p => p.Name == m.Groups["first"].Value).Base = d.DataTypes.Find(p => p.Name == m.Groups["second"].Value
                || removeGenerics.Match(p.Name).Value == m.Groups["second"].Value);
                }
                else if (composition.IsMatch(line))
                {
                    var m = composition.Match(line);

                    if (m.Groups["text"].Success)
                    {
                        var propType = d.DataTypes.Find(p => p.Name == m.Groups["second"].Value);

                        var fromType = d.DataTypes.Find(p => p.Name == m.Groups["first"].Value);

                        if (!fromType.Properties.Any(p => p.Name == m.Groups["text"].Value.Trim()))
                        {
                            ListTypes l = ListTypes.None;
                            if (m.Groups["fm"].Success)
                            {
                            }
                            if (m.Groups["sm"].Success)
                            {
                                if (m.Groups["sm"].Value == "*")
                                {
                                    l = ListTypes.List;
                                }
                            }

                            fromType.Properties.Add(new UMLProperty(m.Groups["text"].Value.Trim(), propType, UMLVisibility.Public, ListTypes.None));
                        }
                    }
                }

                if (DataType != null && line.EndsWith("{"))
                {
                    currentPackage.Children.Add(DataType);
                    while ((line = await sr.ReadLineAsync()) != null)
                    {
                        line = line.Trim();

                        if (line == "}")
                        {
                            if (brackets.Peek() != PACKAGE)
                                brackets.Pop();
                            break;
                        }

                        TryParseLineForDataType(line, aliases, DataType);
                    }
                }
            }

            return d;
        }

        private static UMLVisibility ReadVisibility(string item)
        {
            if (item == "-")
                return UMLVisibility.Private;
            else if (item == "#")
                return UMLVisibility.Protected;
            else if (item == "+")
                return UMLVisibility.Public;

            return UMLVisibility.Public;
        }

        public static async Task<UMLClassDiagram> ReadFile(string file)
        {
            using (StreamReader sr = new StreamReader(file))
            {
                UMLClassDiagram c = await ReadClassDiagram(sr, file);

                return c;
            }
        }

        public static async Task<UMLClassDiagram> ReadString(string s)
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

        public static void TryParseLineForDataType(string line, Dictionary<string, UMLDataType> aliases, UMLDataType DataType)
        {
            var methodMatch = _classLine.Match(line);
            if (methodMatch.Success)
            {
                UMLVisibility visibility = ReadVisibility(methodMatch.Groups["visibility"].Value);
                string name = methodMatch.Groups["name"].Value;

                string returntype = string.Empty;
                for(var x = 0; x < methodMatch.Groups["type"].Captures.Count; x++)
                {
                    if (x != 0)
                        returntype += " ";
                    returntype += methodMatch.Groups["type"].Captures[x].Value;
                }
                returntype = returntype.Trim();

              
                string modifier = methodMatch.Groups["modifier"].Value;

                UMLDataType returnType;

                if (aliases.ContainsKey(returntype))
                {
                    returnType = aliases[returntype];
                }
                else
                {
                    returnType = new UMLDataType(returntype, string.Empty);
                    aliases.Add(returntype, returnType);
                }

                List<UMLParameter> pars = new List<UMLParameter>();

                Stack<char> p = new Stack<char>();
                StringBuilder pname = new StringBuilder();
                StringBuilder sbtype = new StringBuilder();
                bool inName = false;
                string v = methodMatch.Groups["params"].Value;
                for(var x =0; x < v.Length; x++)
                {
                    char c = v[x];
                    if (c == '<')
                        p.Push(c);
                    else if (c == '>')
                        p.Pop();

                 

                    if(c == ' ' && p.Count == 0)
                    {
                        if (!inName)
                        {
                            inName = true;
                        }
                    }

                    else if((c == ',' || x == v.Length - 1) && p.Count == 0)
                    {
                        if (c != ',')
                            pname.Append(c);

                        Tuple<ListTypes, string> d = CreateFrom(sbtype.ToString().Trim());

                        UMLDataType paramType;

                        if (aliases.ContainsKey(d.Item2))
                        {
                            paramType = aliases[d.Item2];
                        }
                        else
                        {
                            paramType = new UMLDataType(d.Item2, string.Empty);
                            aliases.Add(d.Item2, paramType);
                        }

                        pars.Add(new UMLParameter(pname.ToString().Trim(), paramType,d.Item1));

                        sbtype.Clear();
                        pname.Clear();
                        inName = false;
                        x++;
                    }

                   else if (inName)
                    {
                        pname.Append(c);
                    }
                    else
                    {
                        sbtype.Append(c);
                    }


                }

               

                DataType.Methods.Add(new UMLMethod(name, returnType, visibility, pars.ToArray())
                {
                    IsStatic = modifier == "static"
                });
            }
            else if (_propertyLine.IsMatch(line))
            {
                var g = _propertyLine.Match(line);

                UMLVisibility visibility = ReadVisibility(g.Groups["visibility"].Value);

                Tuple<ListTypes, string> p = CreateFrom(g.Groups["type"].Value);

                UMLDataType c;

                if (aliases.ContainsKey(p.Item2))
                {
                    c = aliases[p.Item2];
                }
                else
                {
                    c = new UMLDataType(p.Item2);
                    aliases.Add(p.Item2, c);
                }

                DataType.Properties.Add(new UMLProperty(g.Groups["name"].Value, c, visibility, p.Item1));
            }
            else
            {
                DataType.Properties.Add(new UMLProperty(line, new UMLDataType(string.Empty), 
                    UMLVisibility.None,  ListTypes.None));
            }
        }
    }
}