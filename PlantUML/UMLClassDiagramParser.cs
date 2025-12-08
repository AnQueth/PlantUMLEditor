using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUML
{
    public static class UMLClassDiagramParser
    {
        private static readonly (string word, ListTypes listType)[] COLLECTIONS = new[]
            {
                ("ireadonlycollection<", ListTypes.IReadOnlyCollection),
                ("list<", ListTypes.List)
            };

        private record CollectionRecord(ListTypes ListType, string Word);

        private const string PACKAGE = "package";

        private static readonly Regex _class = new("""(?<abstract>abstract)*\s*class\s+"*(?<name>[\w\<\>\s\,\?]+?)"*(\s+{|\s+as\s+(?<alias>\w+)\s+{)""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex _classLine = new("""(?<visibility>[\+\-\#\~]*)\s*((?<b>[\{])(?<modifier>\w+)*(?<-b>[\}]))*\s*((?<type>[\w\<\>\[\]\,]+)\s)*\s*(?<name>[\w\.\<\>\"]+)\(\s*(?<params>.*)\)\s*:*\s*((?<type2>[\w\<\>\[\]\,\s]+))*\s*((?<b>[\{])(?<modifier>\w+)*(?<-b>[\}]))*""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex _packageRegex = new("(package|together) \\\"*(?<package>[\\w\\s\\.\\-]+)\\\"*[\\w\\W]*\\{", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex _propertyLine = new(@"^\s*((?<visibility>[\+\-\~\#])*\s*(?<modifier>\{[\w]+\})*\s*(?<type>[\w\<\>\,\[\] \?]+)\s+(?<name>[\w_]+)(:(?<defaultvalue>.+))*)|((?<visibility>[\+\-\~\#])*\s*(?<modifier>\{[\w]+\})*\s*(?<name>[\w_]+)\s*:\s*(?<type>[\w\<\>\,\[\] \?]+))(\s+(?<defaultvalue>.+))*\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex _propertyLine2 = new(@"^\s*((?<visibility>[\+\-\~\#])*\s*(?<type>[\w\<\>\,\[\] \?]+)\s+(?<name>[\w_]+))\s*(?<defaultvalue>[\s\w\=]+)*\s*(?<modifier>\{[\w]+\})*|((?<visibility>[\+\-\~\#])*\s*(?<name>[\w_]+)\s*:\s*(?<type>[\w\<\>\,\[\] \?]+))(\[(?<list>[0-9\.\*]+)\])*\s*(?<defaultvalue>[\s\w\=]+)*\s*(?<modifier>\{[\w]+\})*\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);


        private static readonly Regex _baseClass = new("(?<first>\\w+)(\\<((?<generics1>[\\s\\w]+)\\,*)*\\>)*\\s+(?<arrow>[\\-\\.\\|\\>\\<\\*]+)\\s+(?<second>[\\w]+)(\\<((?<generics2>[\\s\\w]+)\\,*)*\\>)*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex _composition = new("""(?<first>\w+)( | \"(?<fm>[01\*\.]+)\" )(?<arrow>[\*o\|\<]*[\-\.]+[\*o\|\>]*)( | \"(?<sm>[01\*\.\w]+)\" )(?<second>\w+) *:*(?<text>.*)""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

      
        private static readonly Regex _rtypeSuffix = new(@"\)\s*:\s*(?<rtype>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
            // estimate capacity: sum of package name lengths + separators
            int estimated = 0;
            foreach (var it in packages)
                estimated += (it?.Length ?? 0) + 1;

            var sb = StringBuilderPool.Rent(Math.Max(16, estimated));
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

            string res = sb.ToString();
            StringBuilderPool.Return(sb);
            return res;
        }

        // collapse multiple whitespace to a single space (avoid Regex.Replace allocations)
        private static string CollapseSpaces(ReadOnlySpan<char> s)
        {
            if (s.Length == 0) return string.Empty;

            // First pass: compute resulting length
            int outLen = 0;
            bool lastSpace = false;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastSpace)
                    {
                        outLen++;
                        lastSpace = true;
                    }
                }
                else
                {
                    outLen++;
                    lastSpace = false;
                }
            }

            if (outLen == s.Length)
            {
                // no change required
                return new string(s);
            }

            // Second pass: fill the string directly
            return string.Create(outLen, s, (span, src) =>
            {
                int idx = 0;
                bool last = false;
                for (int i = 0; i < src.Length; i++)
                {
                    char ch = src[i];
                    if (char.IsWhiteSpace(ch))
                    {
                        if (!last)
                        {
                            span[idx++] = ' ';
                            last = true;
                        }
                    }
                    else
                    {
                        span[idx++] = ch;
                        last = false;
                    }
                }
            });
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

                    if (m.Groups["arrow"].ValueSpan.Contains("<", StringComparison.InvariantCulture))
                    {
                        var f = first;
                        first = second;
                        second = f;
                    }

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
                        if (cl.Name == i.Name)
                        {
                            d.Errors.Add(new UMLError("Base class cannot be the same as the derived class", second, lineNumber));
                            continue;
                        }
                        if (i is UMLInterface ii)
                            cl.Interfaces.Add(ii);
                        else
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
                        string second2 = m.Groups["second"].Value;

                        UMLDataType? propTyper = d.DataTypes.Find(p => string.Equals(p.NonGenericName, second2, StringComparison.Ordinal));
                        if (propTyper == null)
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
                                    if (m.Groups["sm"].ValueSpan.Contains("*", StringComparison.InvariantCulture) ||
                                        m.Groups["sm"].ValueSpan.Contains("many", StringComparison.InvariantCulture))
                                    {
                                        l = ListTypes.List;
                                    }
                                }

                                fromType.Properties.Add(new UMLProperty(m.Groups["text"].Value.Trim(), 
                                    propTyper, UMLVisibility.Public, l, false, false, true, null));
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
                    bool inComment = false;
                    while ((line = await sr.ReadLineAsync()) != null)
                    {
                        lineNumber++;
                        line = line.Trim();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        if(line == "/'")
                        {
                            inComment = true;
                            continue;
                        }
                        if(line == "'/")
                        {
                            inComment = false;
                            continue;
                        }
                        if (inComment)
                        {
                            continue;
                        }

                        if (line.StartsWith('\''))
                            continue;

                        if (line == "}")
                        {
                            if (brackets.Peek() != PACKAGE)
                            {
                                _ = brackets.Pop();
                            }

                            break;
                        }

                        TryParseLineForDataType(d, line, aliases, currentDataType);
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

        public static void TryParseLineForDataType(UMLClassDiagram? diagram, string line, Dictionary<string, UMLDataType> aliases, UMLDataType dataType)
        {
            Match? methodMatch = _classLine.Match(line);
            if (methodMatch.Success)
            {
                UMLVisibility visibility = ReadVisibility(methodMatch.Groups["visibility"].Value);
                string name = methodMatch.Groups["name"].Value;

                // build return type from captures using StringBuilder to reduce allocations
                var rtBuilder = StringBuilderPool.Rent();
                for (int x = 0; x < methodMatch.Groups["type"].Captures.Count; x++)
                {
                    if (x != 0)
                    {
                        rtBuilder.Append(' ');
                    }

                    rtBuilder.Append(methodMatch.Groups["type"].Captures[x].Value);
                }
                string returntype = rtBuilder.ToString().Trim();
                StringBuilderPool.Return(rtBuilder);

                var rt2Builder = StringBuilderPool.Rent();
                for (int x = 0; x < methodMatch.Groups["type2"].Captures.Count; x++)
                {
                    if (x != 0)
                    {
                        rt2Builder.Append(' ');
                    }
                    rt2Builder.Append(methodMatch.Groups["type2"].Captures[x].Value);
                }
                string returntype2 = rt2Builder.ToString().Trim();
                StringBuilderPool.Return(rt2Builder);

                if (!string.IsNullOrWhiteSpace(returntype2))
                {
                    returntype = returntype2;
                }

                // If return type wasn't captured in the C#-style group, attempt to read UML-style ") : type" suffix.
                if (string.IsNullOrWhiteSpace(returntype))
                {
                    var m = _rtypeSuffix.Match(line);
                    if (m.Success)
                    {
                        returntype = m.Groups["rtype"].Value.Trim();
                    }
                }

                string modifier = methodMatch.Groups["modifier"].Value;

                UMLDataType? returnType;

                if (!aliases.TryGetValue(returntype, out returnType))
                {
                    returnType = new UMLDataType(returntype, string.Empty);
                    aliases[returntype] = returnType;
                }

                List<UMLParameter> pars = new();

                // Robust parameter tokenization that supports:
                // - C# style: "int id, Product dto"
                // - UML style: "id:int, dto:Product"
                // - Generics with angle brackets (no splitting inside <>)
                string v = methodMatch.Groups["params"].Value;
                v = CollapseSpaces(v.AsSpan());

                List<string> tokens = new();
                var current = StringBuilderPool.Rent();
                int genericDepth = 0;
                for (int i = 0; i < v.Length; i++)
                {
                    char ch = v[i];
                    if (ch == '<')
                    {
                        genericDepth++;
                        current.Append(ch);
                    }
                    else if (ch == '>')
                    {
                        if (genericDepth > 0) genericDepth--;
                        current.Append(ch);
                    }
                    else if (ch == ',' && genericDepth == 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(ch);
                    }
                }
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                }

                // return the pooled builder used for token accumulation
                StringBuilderPool.Return(current);

                foreach (var token in tokens)
                {
                    string tkn = token.Trim();
                    if (string.IsNullOrEmpty(tkn))
                    {
                        continue;
                    }

                    string paramName = string.Empty;
                    string typeString = string.Empty;

                    // UML style: "name:type"
                    int colonIndex = -1;
                    int depth = 0;
                    for (int i = 0; i < tkn.Length; i++)
                    {
                        char ch = tkn[i];
                        if (ch == '<') depth++;
                        else if (ch == '>') depth = Math.Max(0, depth - 1);
                        else if (ch == ':' && depth == 0)
                        {
                            colonIndex = i;
                            break;
                        }
                    }

                    if (colonIndex != -1)
                    {
                        paramName = tkn[..colonIndex].Trim();
                        typeString = tkn[(colonIndex + 1)..].Trim();
                    }
                    else
                    {
                        // Fallback to C# style: "type name" or "ref/out type name"
                        // find last space that is not inside generics
                        int lastSpace = -1;
                        depth = 0;
                        for (int i = tkn.Length - 1; i >= 0; i--)
                        {
                            char ch = tkn[i];
                            if (ch == '>') depth++;
                            else if (ch == '<') depth = Math.Max(0, depth - 1);
                            else if (ch == ' ' && depth == 0)
                            {
                                lastSpace = i;
                                break;
                            }
                        }

                        if (lastSpace != -1)
                        {
                            paramName = tkn[(lastSpace + 1)..].Trim();
                            typeString = tkn[..lastSpace].Trim();
                        }
                        else
                        {
                            // If we cannot split, treat whole token as name (no type)
                            paramName = tkn;
                            typeString = string.Empty;
                        }
                    }

                    // Normalize typeString (if empty, keep as empty string)
                    CollectionRecord collection = CreateFrom(typeString);

                    UMLDataType? paramType;
                    if (!aliases.TryGetValue(collection.Word, out paramType))
                    {
                        paramType = new UMLDataType(collection.Word, string.Empty);
                        aliases[collection.Word] = paramType;
                    }

                    pars.Add(new UMLParameter(paramName.Trim(), paramType, collection.ListType));
                }

                dataType.Methods.Add(new UMLMethod(name, returnType, visibility, pars.ToArray())
                {
                    IsStatic = modifier == "static",
                    IsAbstract = modifier == "abstract"
                });
            }
            else if(_propertyLine2.IsMatch(line))
            {
                Match? g = _propertyLine2.Match(line);

                UMLVisibility visibility = ReadVisibility(g.Groups["visibility"].Value);

                string modifier = g.Groups["modifier"].Value;

                CollectionRecord? p = CreateFrom(g.Groups["type"].Value);

                UMLDataType? c;

                if (!aliases.TryGetValue(p.Word, out c))
                {
                    c = new UMLDataType(p.Word);
                    aliases[p.Word] = c;
                }

                var isList = g.Groups["list"].ValueSpan.Trim().Length != 0;
                if(isList)
                {
                    p = new CollectionRecord(ListTypes.List, p.Word);
                }

                string defaultValue = g.Groups["defaultvalue"].Value;

                dataType.Properties.Add(new UMLProperty(g.Groups["name"].Value, c, 
                    visibility, p.ListType, modifier == "{static}", modifier == "{abstract}", false, defaultValue));

            }
            else if (_propertyLine.IsMatch(line))
            {
                Match? g = _propertyLine.Match(line);

                UMLVisibility visibility = ReadVisibility(g.Groups["visibility"].Value);

                string modifier = g.Groups["modifier"].Value;

                CollectionRecord? p = CreateFrom(g.Groups["type"].Value);

                UMLDataType? c;

                if (!aliases.TryGetValue(p.Word, out c))
                {
                    c = new UMLDataType(p.Word);
                    aliases[p.Word] = c;
                }

                string defaultValue = g.Groups["defaultvalue"].Value;

                dataType.Properties.Add(new UMLProperty(g.Groups["name"].Value, c,
                    visibility, p.ListType, modifier == "{static}", modifier == "{abstract}", false, defaultValue));
            }
            else if (dataType is UMLEnum)
            {
                string enumValue = line.Trim().TrimEnd(',');
                if (!string.IsNullOrEmpty(enumValue))
                {
                    dataType.Properties.Add(new UMLProperty(enumValue, new UMLDataType("int"), UMLVisibility.Public, ListTypes.None, false, false, false, null));
                }

            }
            else
            {
                diagram?.Errors.Add(new UMLError("Could not parse method or property line", line, dataType.LineNumber));

            }
        }
    }
}