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

        private static readonly Regex _class = new("(?<abstract>abstract)*\\s*class\\s+\"*(?<name>[\\w\\<\\>\\s\\,\\?]+)\"*(\\s+{|\\s+as\\s+(?<alias>\\w+)\\s+{)", RegexOptions.Compiled);

        private static readonly Regex _classLine = new("(?<visibility>[\\+\\-\\#\\~]*)\\s*((?<b>[\\{])(?<modifier>\\w+)*(?<-b>[\\}]))*\\s*((?<type>[\\w\\<\\>\\[\\]\\,]+)\\s)*\\s*(?<name>[\\w\\.\\<\\>\\\"]+)\\(\\s*(?<params>.*)\\)", RegexOptions.Compiled);

        private static readonly Regex _packageRegex = new("(package|together) \\\"*(?<package>[\\w\\s\\.\\-]+)\\\"*[\\w\\W]*\\{", RegexOptions.Compiled);

        private static readonly Regex _propertyLine = new(@"^\s*(?<visibility>[\+\-\~\#])*\s*(?<modifier>\{[\w]+\})*\s*(?<type>[\w\<\>\,\[\] \?]+)\s+(?<name>[\w_]+)\s*$", RegexOptions.Compiled);

        private static readonly Regex _baseClass = new("(?<first>\\w+)(\\<((?<generics1>[\\s\\w]+)\\,*)*\\>)*\\s+(?<arrow>[\\-\\.\\|\\>]+)\\s+(?<second>[\\w]+)(\\<((?<generics2>[\\s\\w]+)\\,*)*\\>)*", RegexOptions.Compiled);

        private static readonly Regex _composition = new("(?<first>\\w+)( | \\\"(?<fm>[01\\*])\\\" )(?<arrow>[\\*o\\|\\<]*[\\-\\.]+[\\*o\\|\\>]*)( | \\\"(?<sm>[01\\*])\\\" )(?<second>\\w+) *:*(?<text>.*)", RegexOptions.Compiled);


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



            Stack<string> brackets = new();
            Regex removeGenerics = new("\\w+");
            Stack<UMLPackage> packagesStack = new();

            CommonParsings cp = new();
            packagesStack.Push(defaultPackage);
            UMLPackage? currentPackage = defaultPackage;
            int lineNumber = 0;
            while ((line = await sr.ReadLineAsync()) != null)
            {
                lineNumber++;

                line = line.Trim();

                if (cp.ParseStart(line, (str) =>
                {
                    d.Title = line[9..].Trim();
                }))
                {
                    started = true;
                    continue;
                }
                if (!started)
                {
                    continue;
                }

                if (cp.ParseTitle(line, (str) =>
                {
                    d.Title = str;

                }))
                {
                    continue;
                }

                if (cp.CommonParsing(line, (str) =>
                {
                    currentPackage.Children.Add(new UMLOther(str));
                },
                (str, alias) =>
                {
                    currentPackage.Children.Add(new UMLNote(str, alias));
                },
                (str) =>
                {
                    currentPackage.Children.Add(new UMLOther(str));
                },
                (str) =>
                {
                    currentPackage.Children.Add(new UMLComment(str));
                },
                (str) =>
                {
                    currentPackage.Children.Add(new UMLOther(str));
                }
                ))
                {
                    continue;
                }


                if (!started)
                {
                    continue;
                }







                if (line.StartsWith("participant", StringComparison.Ordinal)
                    || line.StartsWith("actor", StringComparison.Ordinal))
                {
                    return null;
                }

                UMLDataType? currentDataType = null;

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
                    string name = Clean(g.Groups["name"].Value);
                    string stereotype = string.Empty;
                    var ix = name.IndexOf(" <<", StringComparison.Ordinal);
                    if (ix != -1)
                    {

                        var eix = name.IndexOf(">>", ix, StringComparison.Ordinal);
                        if (eix != -1)
                        {
                            stereotype = name[(ix + 3)..eix];
                            name = name[..ix].Trim();
                        }
                    }
                    string? alias = g.Groups["alias"].Length > 0 ? g.Groups["alias"].Value : null;

                    currentDataType = new UMLClass(stereotype, package, alias, 
                        !string.IsNullOrEmpty(g.Groups["abstract"].Value)
                        , name, new List<UMLDataType>());


                    if (alias != null)
                    {
                        aliases.Add(alias, currentDataType);
                    }

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
                        currentDataType = new UMLEnum(package, Clean(line[5..]));
                    }

                    if (line.EndsWith("{", StringComparison.Ordinal))
                    {
                        brackets.Push("interface");
                    }
                }
                else if (line.StartsWith("struct", StringComparison.Ordinal))
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
                            name = Clean(line[6..line.IndexOf(" as ", StringComparison.Ordinal)]);
                        }
                        else
                        {
                            name = Clean(line[6..]).Replace("\"", string.Empty);
                        }

                        currentDataType = new UMLStruct(package, name, alias
                          , new List<UMLDataType>());

                        if (alias != null)
                        {
                            aliases.Add(alias, currentDataType);
                        }
                    }
                    if (line.EndsWith("{", StringComparison.Ordinal))
                    {
                        brackets.Push("struct");
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

                        currentDataType = new UMLInterface(package, name, alias
                          ,
                            new List<UMLDataType>());

                        if (alias != null)
                        {
                            aliases.Add(alias, currentDataType);
                        }
                    }
                    if (line.EndsWith("{", StringComparison.Ordinal))
                    {
                        brackets.Push("interface");
                    }
                }
                else if (_baseClass.IsMatch(line))
                {
                    Match? m = _baseClass.Match(line);

                    string first = m.Groups["first"].Value;
                    string second = m.Groups["second"].Value;
                    UMLDataType? cl = d.DataTypes.FirstOrDefault(p => p.Name == first);

                    if (cl == null && d.Notes.FirstOrDefault(z => z.Alias == first) == null)
                    {
                        if (!aliases.TryGetValue(m.Groups["first"].Value, out cl))
                        {
                            d.Errors.Add(new UMLError("Could not find parent type", first, lineNumber));
                            continue;
                        }
                    }

                    UMLDataType? i = d.DataTypes.FirstOrDefault(p => p.Name == second || removeGenerics.Match(p.Name).Value == second);

                    if (i == null && d.Notes.FirstOrDefault(z => z.Alias == second) == null)
                    {
                        if (!aliases.TryGetValue(m.Groups["second"].Value, out i))
                        {
                            d.Errors.Add(new UMLError("Could not find base type", second, lineNumber));
                            continue;
                        }
                    }
                    if (cl != null && i != null)
                    {
                        if(cl.Name == i.Name)
                        {
                            d.Errors.Add(new UMLError("Base class cannot be the same as the derived class", second, lineNumber));
                            continue;
                        }
                        cl.Bases.Add(i);
                    }
                    else
                    {
                        d.AddNoteConnection(new UMLNoteConnection(first, m.Groups["arrow"].Value, second));
                    }
                    continue;
                }
                else if (_composition.IsMatch(line))
                {
                    Match? m = _composition.Match(line);

                    if (m.Groups["text"].Success)
                    {
                        string second = m.Groups["second"].Value;

                        UMLDataType? propType = d.DataTypes.Find(p => p.NonGenericName == second);
                        if (propType == null)
                        {
                            d.AddLineError(line, lineNumber);


                        }
                        else
                        {

                            string first = m.Groups["first"].Value.Trim();

                            UMLDataType? fromType = d.DataTypes.Find(p => p.NonGenericName == first);

                            if (fromType is null)
                            {
                                d.AddLineError(line, lineNumber);

                            }


                            else if (!fromType.Properties.Any(p => p.Name == first))
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

                                fromType.Properties.Add(new UMLProperty(m.Groups["text"].Value.Trim(), propType, UMLVisibility.Public, l, false, false, true));
                            }
                        }
                        continue;
                    }
                }

                if (currentDataType != null && line.EndsWith("{", StringComparison.Ordinal))
                {
                    if (aliases.TryGetValue(currentDataType.Name, out UMLDataType? newType))
                    {
                        newType.Namespace = currentDataType.Namespace;
                    }
                    else
                    {
                        aliases.Add(currentDataType.Name, currentDataType);
                    }


                    currentDataType.LineNumber = lineNumber;
                    currentPackage.Children.Add(currentDataType);
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

                        TryParseLineForDataType(line, aliases, currentDataType);
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
            else if (item == "~")
            {
                return UMLVisibility.Internal;
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
                    IsStatic = modifier == "static",
                    IsAbstract = modifier == "abstract"

                });
            }
            else if (_propertyLine.IsMatch(line))
            {
                Match? g = _propertyLine.Match(line);

                UMLVisibility visibility = ReadVisibility(g.Groups["visibility"].Value);

                string modifier = g.Groups["modifier"].Value;

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

                dataType.Properties.Add(new UMLProperty(g.Groups["name"].Value, c, visibility, p.ListType, modifier == "{static}", modifier == "{abstract}", false));
            }
            else
            {
                dataType.Properties.Add(new UMLProperty(line, new UMLDataType(string.Empty),
                    UMLVisibility.None, ListTypes.None, false, false, false));
            }
        }
    }
}