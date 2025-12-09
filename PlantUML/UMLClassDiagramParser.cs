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

        private static readonly Regex _removeGenerics = new("\\w+", RegexOptions.Compiled);

        private static string Clean(ReadOnlySpan<char> name)
        {
            ReadOnlySpan<char> t = name.Trim();
            int endIndex = t.LastIndexOf('{');
            if (endIndex != -1)
            {
                t = t[..endIndex];
            }
            return t.Trim().ToString();
        }

        private static CollectionRecord CreateFrom(ReadOnlySpan<char> v)
        {
            ReadOnlySpan<char> span = v.Trim();

            foreach ((string word, ListTypes listType) in COLLECTIONS)
            {
                if (span.StartsWith(word.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    ReadOnlySpan<char> tmp = span[word.Length..];
                    return new CollectionRecord(listType, tmp[..^1].ToString());
                }
            }

            if (span.EndsWith("[]", StringComparison.Ordinal))
            {
                return new CollectionRecord(ListTypes.Array, span[..^2].ToString());
            }
            else
            {
                return new CollectionRecord(ListTypes.None, span.ToString());
            }
        }

        private static string GetPackage(Stack<string> packages)
        {
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
                return new string(s);
            }

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

        private static void ExtractStereotypeAndName(ReadOnlySpan<char> name, out string stereotype, out string cleanName)
        {
            stereotype = string.Empty;
            int ix = name.IndexOf(" <<", StringComparison.Ordinal);
            if (ix != -1)
            {
                int eix = name[(ix + 3)..].IndexOf(">>", StringComparison.Ordinal);
                if (eix != -1)
                {
                    eix += ix + 3;
                    stereotype = name[(ix + 3)..eix].Trim().ToString();
                    cleanName = name[..ix].Trim().ToString();
                    return;
                }
            }
            cleanName = name.Trim().ToString();
        }

        private static void ExtractAliasAndName(ReadOnlySpan<char> line, int startIndex, int endIndex, out string? alias, out string name)
        {
            alias = null;
            ReadOnlySpan<char> searchSpan = line[startIndex..endIndex];
            int asIndex = searchSpan.IndexOf(" as ", StringComparison.Ordinal);
            if (asIndex != -1)
            {
                asIndex += startIndex;
                ReadOnlySpan<char> remainingLine = line[(asIndex + 4)..];
                // Trim end manually since Span doesn't have TrimEnd with chars
                while (remainingLine.Length > 0 && (remainingLine[^1] == ' ' || remainingLine[^1] == '{'))
                {
                    remainingLine = remainingLine[..^1];
                }
                alias = remainingLine.ToString();
                ReadOnlySpan<char> beforeAs = line[startIndex..asIndex];
                name = Clean(beforeAs);
            }
            else
            {
                name = Clean(line[startIndex..endIndex]);
                alias = null;
            }
        }

        /// <summary>
        /// Extract name and alias from a regex match group span with minimal allocations
        /// </summary>
        private static void ExtractNameFromMatchGroup(ReadOnlySpan<char> groupSpan, out string name, out string? alias)
        {
            alias = null;
            int asIndex = groupSpan.IndexOf(" as ", StringComparison.Ordinal);
            if (asIndex != -1)
            {
                ReadOnlySpan<char> beforeAs = groupSpan[..asIndex].Trim();
                ReadOnlySpan<char> afterAs = groupSpan[(asIndex + 4)..].Trim();
                name = Clean(beforeAs);
                alias = afterAs.ToString();
            }
            else
            {
                name = Clean(groupSpan);
            }
        }

        private static async Task<UMLClassDiagram?> ReadClassDiagram(TextReader sr, string fileName)
        {
            UMLPackage defaultPackage = new("");

            UMLClassDiagram d = new(string.Empty, fileName, defaultPackage);
            bool started = false;
            string? line = null;

            HashSet<string> readPackges = new();
            Stack<string> packages = new();
            Dictionary<string, UMLDataType> aliases = new();

            Stack<string> brackets = new();
            Stack<UMLPackage> packagesStack = new();

            CommonParsings cp = new();
            packagesStack.Push(defaultPackage);
            UMLPackage? currentPackage = defaultPackage;
            int lineNumber = 0;
            
            while ((line = await sr.ReadLineAsync()) != null)
            {
                lineNumber++;
                line = line.Trim();
                ReadOnlySpan<char> lineSpan = line.AsSpan();

                if (cp.ParseStart(line, (str) =>
                {
                    d.Title = line.AsSpan()[9..].Trim().ToString();
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

                if (lineSpan.StartsWith("participant", StringComparison.Ordinal)
                    || lineSpan.StartsWith("actor", StringComparison.Ordinal))
                {
                    return null;
                }

                UMLDataType? currentDataType = null;

                if (line == "}" && brackets.Count > 0 && brackets.Peek() == PACKAGE)
                {
                    _ = brackets.Pop();
                    _ = packages.Pop();
                    _ = packagesStack.Pop();
                    currentPackage = packagesStack.Peek();
                }

                if (lineSpan.StartsWith("title", StringComparison.Ordinal))
                {
                    if (line.Length > 6)
                    {
                        d.Title = lineSpan[6..].ToString();
                    }
                    continue;
                }
                else if (_packageRegex.IsMatch(line))
                {
                    Match? s = _packageRegex.Match(line);
                    string pn = Clean(s.Groups[PACKAGE].Value);

                    if (!readPackges.Add(pn))
                    {
                        d.Errors.Add(new UMLError($"Package {pn} exists already and will cause rendering issues",
                            string.Empty, lineNumber));
                    }
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
                    var nameGroup = g.Groups["name"];
                    if (nameGroup.Length == 0)
                    {
                        continue;
                    }

                    string package = GetPackage(packages);
                    ExtractStereotypeAndName(nameGroup.ValueSpan, out string stereotype, out string name);
                    var aliasGroup = g.Groups["alias"];
                    string? alias = aliasGroup.Length > 0 ? aliasGroup.Value : null;

                    currentDataType = new UMLClass(stereotype, package, alias,
                        !string.IsNullOrEmpty(g.Groups["abstract"].Value), name, new List<UMLDataType>());

                    if (alias != null)
                    {
                        aliases.Add(alias, currentDataType);
                    }

                    if (lineSpan.EndsWith("{", StringComparison.Ordinal))
                    {
                        brackets.Push("class");
                    }
                }
                else if (lineSpan.StartsWith("enum", StringComparison.Ordinal))
                {
                    string package = GetPackage(packages);
                    if (line.Length > 4)
                    {
                        currentDataType = new UMLEnum(package, Clean(lineSpan[5..]));
                        aliases[currentDataType.Name] = currentDataType;
                    }

                    if (lineSpan.EndsWith("{", StringComparison.Ordinal))
                    {
                        brackets.Push("interface");
                    }
                }
                else if (lineSpan.StartsWith("struct", StringComparison.Ordinal))
                {
                    string package = GetPackage(packages);
                    if (line.Length > 8)
                    {
                        ExtractAliasAndName(lineSpan, 6, line.Length, out string? alias, out string name);
                        currentDataType = new UMLStruct(package, name, alias, new List<UMLDataType>());

                        if (alias != null)
                        {
                            aliases.Add(alias, currentDataType);
                        }
                    }
                    if (lineSpan.EndsWith("{", StringComparison.Ordinal))
                    {
                        brackets.Push("struct");
                    }
                }
                else if (lineSpan.StartsWith("interface", StringComparison.Ordinal))
                {
                    string package = GetPackage(packages);
                    if (line.Length > 8)
                    {
                        ExtractAliasAndName(lineSpan, 9, line.Length, out string? alias, out string name);
                        currentDataType = new UMLInterface(package, name, alias, new List<UMLDataType>());

                        if (alias != null)
                        {
                            aliases.Add(alias, currentDataType);
                        }
                    }
                    if (lineSpan.EndsWith("{", StringComparison.Ordinal))
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
                        (first, second) = (second, first);
                    }

                    // Manual loop instead of FirstOrDefault to avoid LINQ allocations
                    UMLDataType? cl = null;
                    foreach (var dt in d.DataTypes)
                    {
                        if (dt.Name == first)
                        {
                            cl = dt;
                            break;
                        }
                    }

                    if (cl == null)
                    {
                        bool foundNote = false;
                        foreach (var note in d.Notes)
                        {
                            if (note.Alias == first)
                            {
                                foundNote = true;
                                break;
                            }
                        }
                        
                        if (!foundNote && !aliases.TryGetValue(first, out cl))
                        {
                            d.Errors.Add(new UMLError("Could not find parent type", first, lineNumber));
                            continue;
                        }
                    }

                    // Manual loop instead of FirstOrDefault to avoid LINQ allocations
                    UMLDataType? i = null;
                    foreach (var dt in d.DataTypes)
                    {
                        if (dt.Name == second)
                        {
                            i = dt;
                            break;
                        }
                        if (_removeGenerics.Match(dt.Name).Value == second)
                        {
                            i = dt;
                            break;
                        }
                    }

                    if (i == null)
                    {
                        bool foundNote = false;
                        foreach (var note in d.Notes)
                        {
                            if (note.Alias == second)
                            {
                                foundNote = true;
                                break;
                            }
                        }
                        
                        if (!foundNote && !aliases.TryGetValue(second, out i))
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
                        ReadOnlySpan<char> secondSpan = m.Groups["second"].ValueSpan;
                        string second2 = secondSpan.ToString();

                        // Use Find instead of LINQ: already efficient
                        UMLDataType? propTyper = d.DataTypes.Find(p => string.Equals(p.NonGenericName, second2, StringComparison.Ordinal));
                        if (propTyper == null)
                        {
                            d.AddLineError(line, lineNumber);
                        }
                        else
                        {
                            ReadOnlySpan<char> firstSpan = m.Groups["first"].ValueSpan.Trim();
                            string first = firstSpan.ToString();
                            UMLDataType? fromType = d.DataTypes.Find(p => p.NonGenericName == first);

                            if (fromType is null)
                            {
                                d.AddLineError(line, lineNumber);
                            }
                            else
                            {
                                // Manual loop instead of Any() to avoid LINQ allocation
                                bool propertyExists = false;
                                foreach (var prop in fromType.Properties)
                                {
                                    if (prop.Name == first)
                                    {
                                        propertyExists = true;
                                        break;
                                    }
                                }

                                if (!propertyExists)
                                {
                                    ListTypes l = ListTypes.None;
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
                        }
                        continue;
                    }
                }

                if (currentDataType != null && lineSpan.EndsWith("{", StringComparison.Ordinal))
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

        private static UMLVisibility ReadVisibility(ReadOnlySpan<char> item) => item.Length switch
        {
            1 => item[0] switch
            {
                '-' => UMLVisibility.Private,
                '#' => UMLVisibility.Protected,
                '+' => UMLVisibility.Public,
                '~' => UMLVisibility.Internal,
                _ => UMLVisibility.None
            },
            _ => UMLVisibility.None
        };

        public static async Task<UMLClassDiagram?> ReadFile(string file)
        {
            using StreamReader sr = new(file);
            UMLClassDiagram? c = await ReadClassDiagram(sr, file);

            return c;
        }

        public static async Task<UMLClassDiagram?> ReadString(string s)
        {
          
            using StringReader sr = new(s);
            UMLClassDiagram? c = await ReadClassDiagram(sr, "");

            return c;
        }

        public static void TryParseLineForDataType(UMLClassDiagram? diagram, string line, Dictionary<string, UMLDataType> aliases, UMLDataType dataType)
        {
            Match? methodMatch = _classLine.Match(line);
            if (methodMatch.Success)
            {
                ReadOnlySpan<char> visSpan = methodMatch.Groups["visibility"].ValueSpan;
                UMLVisibility visibility = ReadVisibility(visSpan);
                string name = methodMatch.Groups["name"].Value;

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

                var paramsSpan = methodMatch.Groups["params"].ValueSpan;
                paramsSpan = CollapseSpaces(paramsSpan);

                // Use span-based tokenization instead of List<string> to defer ToString()
                var tokenRanges = new List<(int start, int length)>();

                int tokenStart = 0;
                int genericDepth = 0;
                for (int i = 0; i < paramsSpan.Length; i++)
                {
                    char ch = paramsSpan[i];
                    if (ch == '<')
                    {
                        genericDepth++;
                    }
                    else if (ch == '>')
                    {
                        if (genericDepth > 0) genericDepth--;
                    }
                    else if (ch == ',' && genericDepth == 0)
                    {
                        if (i > tokenStart)
                        {
                            tokenRanges.Add((tokenStart, i - tokenStart));
                        }
                        tokenStart = i + 1;
                    }
                }
                if (tokenStart < paramsSpan.Length)
                {
                    tokenRanges.Add((tokenStart, paramsSpan.Length - tokenStart));
                }

                foreach (var (tStart, tLen) in tokenRanges)
                {
                    ReadOnlySpan<char> tokenSpan = paramsSpan[tStart..(tStart + tLen)].Trim();
                    if (tokenSpan.IsEmpty)
                    {
                        continue;
                    }

                    int colonIndex = -1;
                    int depth = 0;
                    for (int i = 0; i < tokenSpan.Length; i++)
                    {
                        char ch = tokenSpan[i];
                        if (ch == '<') depth++;
                        else if (ch == '>') depth = Math.Max(0, depth - 1);
                        else if (ch == ':' && depth == 0)
                        {
                            colonIndex = i;
                            break;
                        }
                    }

                    ReadOnlySpan<char> paramName;
                    ReadOnlySpan<char> typeSpan;

                    if (colonIndex != -1)
                    {
                        paramName = tokenSpan[..colonIndex].Trim();
                        typeSpan = tokenSpan[(colonIndex + 1)..].Trim();
                    }
                    else
                    {
                        int lastSpace = -1;
                        depth = 0;
                        for (int i = tokenSpan.Length - 1; i >= 0; i--)
                        {
                            char ch = tokenSpan[i];
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
                            paramName = tokenSpan[(lastSpace + 1)..].Trim();
                            typeSpan = tokenSpan[..lastSpace].Trim();
                        }
                        else
                        {
                            paramName = tokenSpan;
                            typeSpan = default;
                        }
                    }

                
                    CollectionRecord collection = CreateFrom(typeSpan);

                    UMLDataType? paramType;
                    if (!aliases.TryGetValue(collection.Word, out paramType))
                    {
                        paramType = new UMLDataType(collection.Word, string.Empty);
                        aliases[collection.Word] = paramType;
                    }

                    pars.Add(new UMLParameter(paramName.Trim().ToString(), paramType, collection.ListType));
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

                ReadOnlySpan<char> visSpan = g.Groups["visibility"].ValueSpan;
                UMLVisibility visibility = ReadVisibility(visSpan);
                string modifier = g.Groups["modifier"].Value;
                CollectionRecord? p = CreateFrom(g.Groups["type"].ValueSpan);

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

                ReadOnlySpan<char> visSpan = g.Groups["visibility"].ValueSpan;
                UMLVisibility visibility = ReadVisibility(visSpan);
                string modifier = g.Groups["modifier"].Value;
                CollectionRecord? p = CreateFrom(g.Groups["type"].ValueSpan);

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