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
    public class UMLClassDiagramParser : IPlantUMLParser
    {
        private static readonly (string word, ListTypes listType)[] COLLECTIONS = new[]
            {
                ("ireadonlycollection<", ListTypes.IReadOnlyCollection),
                ("list<", ListTypes.List)
            };

        private record CollectionRecord(ListTypes ListType, string Word);

        private const string PACKAGE = "package";

        private static readonly Regex _class = new("(?<abstract>abstract)*\\s*class\\s+\"*(?<name>[\\w\\<\\>\\s\\,\\?]+)\"*\\s+{", RegexOptions.Compiled);

        private static readonly Regex _classLine = new("((?<b>[\\{])(?<modifier>\\w+)*(?<-b>[\\}]))*\\s*(?<visibility>[\\+\\-\\#\\~]*)\\s*((?<type>[\\w\\<\\>\\[\\]\\,]+)\\s)*\\s*(?<name>[\\w\\.\\<\\>\\\"]+)\\(\\s*(?<params>.*)\\)", RegexOptions.Compiled);

        private static readonly Regex _packageRegex = new("(package|together) \\\"*(?<package>[\\w\\s\\.\\-]+)\\\"* *\\{", RegexOptions.Compiled);

        private static readonly Regex _propertyLine = new("^\\s*(?<visibility>[\\+\\-\\~\\#])*\\s*(?<type>[\\w\\<\\>\\,\\[\\] \\?]+)\\s+(?<name>[\\w_]+)\\s*$", RegexOptions.Compiled);

        private static readonly Regex baseClass = new("(?<first>\\w+)(\\<((?<generics1>[\\s\\w]+)\\,*)*\\>)*\\s+(?<arrow>[\\-\\.\\|\\>]+)\\s+(?<second>[\\w]+)(\\<((?<generics2>[\\s\\w]+)\\,*)*\\>)*", RegexOptions.Compiled);

        private static readonly Regex composition = new("(?<first>\\w+)( | \\\"(?<fm>[01\\*])\\\" )(?<arrow>[\\*o\\|\\<]*[\\-\\.]+[\\*o\\|\\>]*)( | \\\"(?<sm>[01\\*])\\\" )(?<second>\\w+) *:*(?<text>.*)", RegexOptions.Compiled);

        private static readonly Regex notes = new("note *((?<sl>(?<placement>\\w+) of (?<target>[\\\"\\w\\,\\s\\<\\>]+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\\\"(?<text>[\\w\\W]+)\\\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>[\\\"\\w\\,\\s\\<\\>]+)| as (?<alias>\\w+))", RegexOptions.Compiled);

        private static string Clean(string name)
        {
            string? t = name.Trim();
            return t.TrimEnd('{').Trim();
        }

        private static CollectionRecord CreateFrom(string v)
        {

            v = v.Trim();



            foreach ((string word, ListTypes listType) in COLLECTIONS)
            {
                if (v.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                {
                    string tmp = v[word.Length..];

                    return new CollectionRecord(listType, tmp.Remove(tmp.Length - 1));
                }
            }


            if (v.EndsWith("[]", StringComparison.Ordinal))
            {
                return new CollectionRecord(ListTypes.Array, v[0..^2]);
            }
            else
            {
                return new CollectionRecord(ListTypes.None, v);
            }
        }

        private static string GetPackage(Stack<string> packages)
        {
            StringBuilder sb = new();
            int x = 0;
            foreach (string? item in packages.Reverse())
            {
                _ = sb.Append(item);
                if (x < packages.Count - 1)
                {
                    _ = sb.Append('.');
                }

                x++;
            }

            return sb.ToString();
        }

        private static async Task<UMLClassDiagram?> ReadClassDiagram(StreamReader sr, string fileName)
        {
            UMLPackage defaultPackage = new("");

            UMLClassDiagram d = new(string.Empty, fileName, defaultPackage);
            bool started = false;
            string? line = null;

            List<string> readPackges = new();
            Stack<string> packages = new();

            Dictionary<string, UMLDataType> aliases = new();

            bool swallowingNotes = false;
            bool swallowingComments = false;

            Stack<string> brackets = new();
            Regex removeGenerics = new("\\w+");
            Stack<UMLPackage> packagesStack = new();


            packagesStack.Push(defaultPackage);
            UMLPackage? currentPackage = defaultPackage;
            int lineNumber = 0;
            while ((line = await sr.ReadLineAsync()) != null)
            {
                lineNumber++;

                line = line.Trim();

                if (line == "@startuml")
                {
                    started = true;
                }

                if (!started)
                {
                    continue;
                }

                if (line == "left to right direction")
                {
                    currentPackage.Children.Add(new UMLOther(line));
                    continue;
                }

                if (line.StartsWith("'", StringComparison.Ordinal))
                {
                    currentPackage.Children.Add(new UMLComment(line));
                    continue;
                }

                if (line.StartsWith("/'", StringComparison.Ordinal))
                {
                    string comment = line;
                    swallowingComments = true;
                }

                if (line.Contains("'/", StringComparison.Ordinal) && swallowingComments)
                {
                    if (currentPackage.Children.Last() is UMLComment n)
                    {
                        n.Text += "\r\n" + line;
                    }
                    swallowingComments = false;
                    continue;
                }
                if (swallowingComments)
                {
                    if (currentPackage.Children.Last() is UMLComment n)
                    {
                        n.Text += "\r\n" + line;
                    }
                    continue;
                }
                if (notes.IsMatch(line))
                {
                    Match? m = notes.Match(line);
                    if (!m.Groups["sl"].Success)
                    {
                        swallowingNotes = true;
                    }
                    else
                    {
                        swallowingNotes = false;
                    }

                    currentPackage.Children.Add(new UMLNote(line));
                    continue;
                }

                if (line.StartsWith("end note", StringComparison.Ordinal))
                {
                    if (currentPackage.Children.Last() is UMLNote n)
                    {
                        n.Text += "\r\nend note";
                    }
                    swallowingNotes = false;
                    continue;
                }
                if (swallowingNotes)
                {
                    if (currentPackage.Children.Last() is UMLNote n)
                    {
                        n.Text += "\r\n" + line;
                    }
                    continue;
                }
                if (line.StartsWith("participant", StringComparison.Ordinal)
                    || line.StartsWith("actor", StringComparison.Ordinal))
                {
                    return null;
                }

                UMLDataType? DataType = null;

                if (line == "}" && brackets.Count > 0 && brackets.Peek() == PACKAGE)
                {
                    _ = brackets.Pop();
                    _ = packages.Pop();
                    _ = packagesStack.Pop();
                    currentPackage = packagesStack.First();
                }

                if (line.StartsWith("title", StringComparison.Ordinal))
                {
                    if (line.Length > 6)
                    {
                        d.Title = line[6..];
                    }

                    continue;
                }
                else if (_packageRegex.IsMatch(line))
                {
                    Match? s = _packageRegex.Match(line);

                    string pn = Clean(s.Groups[PACKAGE].Value);

                    if (readPackges.Any(z => z == pn))
                    {
                        d.Errors.Add(new UMLError($"Package {pn} exists already and will cause rendering issues",
                            string.Empty, lineNumber));
                    }
                    readPackges.Add(pn);
                    packages.Push(pn);
                    brackets.Push(PACKAGE);


                    UMLPackage? c = new UMLPackage(pn);



                    currentPackage.Children.Add(c);
                    currentPackage = c;

                    packagesStack.Push(c);


                    continue;
                }
                else if (_class.IsMatch(line))
                {
                    Match? g = _class.Match(line);
                    if (string.IsNullOrEmpty(g.Groups["name"].Value))
                    {
                        continue;
                    }

                    string package = GetPackage(packages);

                    DataType = new UMLClass(package, !string.IsNullOrEmpty(g.Groups["abstract"].Value)
                        , Clean(g.Groups["name"].Value), new List<UMLDataType>());

                    if (line.EndsWith("{", StringComparison.Ordinal))
                    {
                        brackets.Push("class");
                    }
                }
                else if (line.StartsWith("enum", StringComparison.Ordinal))
                {
                    string package = GetPackage(packages);
                    if (line.Length > 4)
                    {
                        DataType = new UMLEnum(package, Clean(line[5..]));
                    }

                    if (line.EndsWith("{", StringComparison.Ordinal))
                    {
                        brackets.Push("interface");
                    }
                }
                else if (line.StartsWith("interface", StringComparison.Ordinal))
                {
                    string package = GetPackage(packages);
                    if (line.Length > 8)
                    {
                        string? alias = null;
                        string? name = null;

                        if (line.Contains(" as "))
                        {
                            line = line.Replace("\"", string.Empty);
                            alias = line[(line.IndexOf(" as ", StringComparison.Ordinal) + 4)..].TrimEnd(' ', '{');
                            name = Clean(line[9..line.IndexOf(" as ", StringComparison.Ordinal)]);
                        }
                        else
                        {
                            name = Clean(line[9..]).Replace("\"", string.Empty);
                        }

                        DataType = new UMLInterface(package, name
                          ,
                            new List<UMLDataType>());

                        if (alias != null)
                        {
                            aliases.Add(alias, DataType);
                        }
                    }
                    if (line.EndsWith("{", StringComparison.Ordinal))
                    {
                        brackets.Push("interface");
                    }
                }
                else if (baseClass.IsMatch(line))
                {
                    Match? m = baseClass.Match(line);
                    UMLDataType? cl = d.DataTypes.FirstOrDefault(p => p.Name == m.Groups["first"].Value);
                    if (cl == null)
                    {
                        d.Errors.Add(new UMLError("Could not find parent type", m.Groups["first"].Value, lineNumber));
                    }
                    UMLDataType? i = d.DataTypes.FirstOrDefault(p => p.Name == m.Groups["second"].Value
                || removeGenerics.Match(p.Name).Value == m.Groups["second"].Value);
                    if (i == null)
                    {
                        if (!aliases.TryGetValue(m.Groups["second"].Value, out i))
                        {
                            d.Errors.Add(new UMLError("Could not find base type", m.Groups["second"].Value, lineNumber));
                        }
                    }
                    if (cl != null && i != null)
                    {
                        cl.Bases.Add(i);
                    }
                    continue;
                }
                else if (composition.IsMatch(line))
                {
                    Match? m = composition.Match(line);

                    if (m.Groups["text"].Success)
                    {
                        UMLDataType? propType = d.DataTypes.Find(p => p.NonGenericName == m.Groups["second"].Value);
                        if (propType == null)
                        {
                            d.AddLineError(line, lineNumber);


                        }
                        else
                        {

                            UMLDataType? fromType = d.DataTypes.Find(p => p.NonGenericName == m.Groups["first"].Value);

                            if (fromType != null && !fromType.Properties.Any(p => p.Name == m.Groups["text"].Value.Trim()))
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

                                fromType.Properties.Add(new UMLProperty(m.Groups["text"].Value.Trim(), propType, UMLVisibility.Public, l));
                            }
                        }
                        continue;
                    }
                }

                if (DataType != null && line.EndsWith("{", StringComparison.Ordinal))
                {
                    if (aliases.TryGetValue(DataType.Name, out UMLDataType? newType))
                    {
                        newType.Namespace = DataType.Namespace;
                    }
                    else
                    {
                        aliases.Add(DataType.Name, DataType);
                    }


                    DataType.LineNumber = lineNumber;
                    currentPackage.Children.Add(DataType);
                    while ((line = await sr.ReadLineAsync()) != null)
                    {
                        lineNumber++;
                        line = line.Trim();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        if (line == "}")
                        {
                            if (brackets.Peek() != PACKAGE)
                            {
                                _ = brackets.Pop();
                            }

                            break;
                        }

                        TryParseLineForDataType(line, aliases, DataType);
                    }
                }
                else
                {
                    d.AddLineError(line, lineNumber);
                }
            }




            return d;
        }

        private static UMLVisibility ReadVisibility(string item)
        {
            if (item == "-")
            {
                return UMLVisibility.Private;
            }
            else if (item == "#")
            {
                return UMLVisibility.Protected;
            }
            else if (item == "+")
            {
                return UMLVisibility.Public;
            }

            return UMLVisibility.None;
        }

        public static async Task<UMLClassDiagram?> ReadFile(string file)
        {
            using StreamReader sr = new(file);
            UMLClassDiagram? c = await ReadClassDiagram(sr, file);

            return c;
        }

        public static async Task<UMLClassDiagram?> ReadString(string s)
        {
            using MemoryStream ms = new(Encoding.UTF8.GetBytes(s));
            using StreamReader sr = new(ms);
            UMLClassDiagram? c = await ReadClassDiagram(sr, "");

            return c;
        }

        public static void TryParseLineForDataType(string line, Dictionary<string, UMLDataType> aliases, UMLDataType dataType)
        {
            Match? methodMatch = _classLine.Match(line);
            if (methodMatch.Success)
            {
                UMLVisibility visibility = ReadVisibility(methodMatch.Groups["visibility"].Value);
                string name = methodMatch.Groups["name"].Value;

                string returntype = string.Empty;
                for (int x = 0; x < methodMatch.Groups["type"].Captures.Count; x++)
                {
                    if (x != 0)
                    {
                        returntype += " ";
                    }

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

                List<UMLParameter> pars = new();

                Stack<char> p = new();
                StringBuilder pname = new();
                StringBuilder sbtype = new();
                bool inName = false;
                string v = methodMatch.Groups["params"].Value;
                v = Regex.Replace(v, "\\s{2,}", " ");
                for (int x = 0; x < v.Length; x++)
                {
                    char c = v[x];
                    if (c == '<')
                    {
                        p.Push(c);
                    }
                    else if (c == '>')
                    {
                        _ = p.Pop();
                    }

                    if (c == ' ' && p.Count == 0)
                    {
                        if (sbtype.ToString() is not "out" and not "ref")
                        {
                            if (!inName)
                            {
                                inName = true;
                            }
                        }
                        else
                        {
                            _ = sbtype.Append(c);
                        }
                    }
                    else if ((c == ',' || x == v.Length - 1) && p.Count == 0)
                    {
                        if (c != ',')
                        {
                            _ = pname.Append(c);
                        }

                        CollectionRecord d = CreateFrom(sbtype.ToString());

                        UMLDataType paramType;

                        if (aliases.ContainsKey(d.Word))
                        {
                            paramType = aliases[d.Word];
                        }
                        else
                        {
                            paramType = new UMLDataType(d.Word, string.Empty);
                            aliases.Add(d.Word, paramType);
                        }

                        pars.Add(new UMLParameter(pname.ToString().Trim(), paramType, d.ListType));

                        _ = sbtype.Clear();
                        _ = pname.Clear();
                        inName = false;
                        x++;
                    }
                    else if (inName)
                    {
                        _ = pname.Append(c);
                    }
                    else
                    {
                        _ = sbtype.Append(c);
                    }
                }

                dataType.Methods.Add(new UMLMethod(name, returnType, visibility, pars.ToArray())
                {
                    IsStatic = modifier == "static"
                });
            }
            else if (_propertyLine.IsMatch(line))
            {
                Match? g = _propertyLine.Match(line);

                UMLVisibility visibility = ReadVisibility(g.Groups["visibility"].Value);

                CollectionRecord? p = CreateFrom(g.Groups["type"].Value);

                UMLDataType c;

                if (aliases.ContainsKey(p.Word))
                {
                    c = aliases[p.Word];
                }
                else
                {
                    c = new UMLDataType(p.Word);
                    aliases.Add(p.Word, c);
                }

                dataType.Properties.Add(new UMLProperty(g.Groups["name"].Value, c, visibility, p.ListType));
            }
            else
            {
                dataType.Properties.Add(new UMLProperty(line, new UMLDataType(string.Empty),
                    UMLVisibility.None, ListTypes.None));
            }
        }
    }
}