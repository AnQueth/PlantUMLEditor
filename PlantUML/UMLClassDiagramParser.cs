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

        private static Regex _class = new Regex("(?<abstract>abstract)*\\s*class\\s+(?<name>[\\w\\<\\>]+)\\s+{");

        private static Regex _classLine = new Regex("((?<b>[\\{])(?<modifier>\\w+)*(?<-b>[\\}]))*\\s*(?<visibility>[\\+\\-\\#\\~]*)\\s*((?<type>[\\w\\<\\>\\[\\]]+)\\s)*\\s*(?<name>[\\w\\.\\<\\>]+)\\((?<params>((?<pt>[\\w\\[\\]]+|\\w+\\<.*\\>?)\\s+(?<pn>\\w+))\\s*,*\\s*)*\\)");

        private static Regex _packageRegex = new Regex("package \\\"*(?<package>[\\w\\s\\.]+)\\\"* *\\{");

        private static Regex _propertyLine = new Regex("^(?<visibility>[\\+\\-\\~\\#])*\\s*(?<type>[\\w\\<\\>]+)\\s+(?<name>[\\w]+)");

        private static Regex baseClass = new Regex("(?<first>\\w+)(\\<((?<generics1>[\\s\\w]+)\\,*)*\\>)*\\s+(?<arrow>[\\-\\.]+)\\s+(?<second>[\\w]+)(\\<((?<generics2>[\\s\\w]+)\\,*)*\\>)*");

        private static Regex composition = new Regex("(?<first>\\w+)( | \\\"(?<fm>[01\\*])\\\" )(?<arrow>[\\*o\\!\\<]*[\\-\\.]+[\\*o\\!\\>]*)( | \\\"(?<sm>[01\\*])\\\" )(?<second>\\w+) *:*(?<text>.*)");

        private static Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))");

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

        private static string GetPackage(Stack<string> packages)
        {
            StringBuilder sb = new StringBuilder();
            int x = 0;
            foreach (var item in packages)
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

                if (line == "}" && brackets.Peek() == PACKAGE)
                {
                    brackets.Pop();
                    packages.Pop();
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

                        fromType.Properties.Add(new UMLProperty(m.Groups["text"].Value, propType, UMLVisibility.Public, ListTypes.None));
                    }
                }

                if (DataType != null && line.EndsWith("{"))
                {
                    string currentPackage = GetPackage(packages);

                    d.DataTypes.Add(DataType);
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
            return UMLVisibility.None;
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
                string returntype = methodMatch.Groups["type"].Value;
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

                for (int x = 0; x < methodMatch.Groups["params"].Captures.Count; x++)
                {
                    string pt = methodMatch.Groups["pt"].Captures[x].Value;
                    string pn = methodMatch.Groups["pn"].Captures[x].Value;

                    Tuple<ListTypes, string> p = CreateFrom(pt);

                    UMLDataType paramType;

                    if (aliases.ContainsKey(p.Item2))
                    {
                        paramType = aliases[p.Item2];
                    }
                    else
                    {
                        paramType = new UMLDataType(p.Item2, string.Empty);
                        aliases.Add(p.Item2, paramType);
                    }

                    pars.Add(new UMLParameter(pn, paramType, p.Item1));
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
        }
    }
}