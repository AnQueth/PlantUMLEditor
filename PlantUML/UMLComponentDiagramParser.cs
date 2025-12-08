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
    public static class UMLComponentDiagramParser
    {
        private const string CLASSNAME = "class";
        private const string PACKAGENAME = "name";

        // Component regex broken into smaller parts for better maintainability
        private static readonly Regex _componentBracketStyle = new(@"^\[(?<name>[^]]+)\]\s*(?:as\s+(?<alias>\w+))?\s*(?:<<(?<stereotype>[\w\s]+)>>)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex _componentType = new(@"^(component|entity|database|queue|actor|rectangle|cloud)\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex _componentName = new(@"^(?:""(?<name>[^""]+)""|(?<name>\w+))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex _componentAlias = new(@"^\s*as\s+(?<alias>\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex _componentColor = new(@"^\s*(?<color>#(?:[0-9A-Fa-f]{3,6}|[A-Za-z]+))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex _interface = new(@"^(\(\)|interface)\s+\""*((?<name>[\w \\]+)\""*(\s+as\s+(?<alias>[\w]+))|(?<name>[\w \\]+)\""*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _packageRegex = new(@"^\s*(?<type>package|frame|node|cloud|node|folder|together|rectangle) +((?<name>[\w]+)|\""(?<name>[\w\s\W]+)\"")\s+as (?<alias>[\w\s]+)*\s+\{", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
        private static readonly Regex _packageRegex2 = new(@"^\s*(?<type>package|frame|node|cloud|folder|together|rectangle)\s+(?:""(?<name>[^""]+)""|(?<name>[^{}\r\n]+?))\s*\{", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
        private static readonly Regex composition = new(@"^(?<leftbracket>\[*)(?<left>[\w ]+?)\]*\s*(?:""(?<leftcard>[\w ]+)""\s*)?(?<arrow>[\<\-\(\)o\[\]=,.\#\|\{\}\*]+(?<direction>[^\s""\]]+)?[\-\>\(\)o\[\].\#\|\{\}]*)\s*(?:""(?<rightcard>[\w ]+)""\s*)?(?<rightbracket>\[*)(?<right>[\w ]+)\]*\s*(?::\s*(?<label>.*))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
        private static readonly Regex _ports = new(@"^\s*(port |portin |portout )(?<name>.*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _removeGenerics = new("\\w+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static ReadOnlySpan<char> Clean(ReadOnlySpan<char> name)
        {
            var t = name.Trim();
            return t.TrimEnd('{').Trim();
        }

        private record struct ComponentParseResult(bool success, string name, string description, string alias, string color);

        private static ComponentParseResult ParseComponentLine(ReadOnlySpan<char> line)
        {
            ReadOnlySpan<char> remaining = line;

            // Match component type
            Match typeMatch = _componentType.Match(remaining.ToString());
            if (!typeMatch.Success)
                return new (false, string.Empty, string.Empty, string.Empty, string.Empty);

            remaining = remaining[(typeMatch.Length)..];

            // Match name
            Match nameMatch = _componentName.Match(remaining.ToString());
            if (!nameMatch.Success)
                return new(false, string.Empty, string.Empty, string.Empty, string.Empty);

            var name = nameMatch.Groups["name"].ValueSpan;
            // Do not TrimStart before marker search to correctly detect leading "as "
            remaining = remaining[nameMatch.Length..];
            name = Clean(name);

            // Try to match description/stereotype (optional) - everything before alias or color marker
            string description = string.Empty;
            // Find alias marker: either " as " with leading space or startswith "as " at beginning
            int asIndex = remaining.IndexOf(" as ", StringComparison.OrdinalIgnoreCase);
            if (asIndex < 0 && remaining.StartsWith("as ", StringComparison.OrdinalIgnoreCase))
                asIndex = 0;

            int colorIndex = remaining.IndexOf(" #", StringComparison.OrdinalIgnoreCase);

            int endIndex = remaining.Length;
            if (asIndex >= 0 && (colorIndex < 0 || asIndex < colorIndex))
                endIndex = asIndex;
            else if (colorIndex >= 0)
                endIndex = colorIndex;

            description = remaining[..endIndex].ToString().Trim();
            remaining = remaining[endIndex..].TrimStart();

            // Try to match alias (optional)
            string alias = string.Empty;
            Match aliasMatch = _componentAlias.Match(remaining.ToString());
            if (aliasMatch.Success)
            {
                alias = aliasMatch.Groups["alias"].Value;
                remaining = remaining[aliasMatch.Length..];
            }

            if(string.IsNullOrEmpty(alias))
            {
                alias = name.ToString();
            }

            // Try to match color (optional)
            string color = string.Empty;
            Match colorMatch = _componentColor.Match(remaining.ToString());
            if (colorMatch.Success)
            {
                color = colorMatch.Groups["color"].Value;
            }

            return new(true, name.ToString(), description, alias, color);
        }

        private static string GetPackage(Stack<string> packages)
        {
            var sb = StringBuilderPool.Rent();
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

        // Changed to accept TextReader to avoid allocating MemoryStream when parsing strings
        private static async Task<UMLComponentDiagram?> ReadComponentDiagram(TextReader sr, 
            string fileName, bool componentsMustBeDefined)
        {

            bool started = false;
            string? line = null;

            Stack<string> packages = new();

            Dictionary<string, UMLDataType> aliases = new();


            Stack<string> brackets = new();
       
            Stack<UMLPackage> packagesStack = new();
            Stack<UMLComponent> components = new();
            UMLComponent? currentComponent = null;

            UMLPackage defaultPackage = new("");

            UMLComponentDiagram diagram = new(string.Empty, fileName, defaultPackage);
            packagesStack.Push(defaultPackage);
            UMLPackage? currentPackage = defaultPackage;

            CommonParsings cp = new CommonParsings();
            int linenumber = 0;

            while ((line = await sr.ReadLineAsync()) != null)
            {
                linenumber++;
                line = line.Trim();

                try
                {
                    // Precompute regex matches so IsMatch isn't called repeatedly in the if/else chain
                    Match mPorts = _ports.Match(line);
                    Match mPackage1 = _packageRegex.Match(line);
                    Match mPackage2 = _packageRegex2.Match(line);
                    Match mComponentBracket = _componentBracketStyle.Match(line);
                    Match mComponentType = _componentType.Match(line);
                    Match mInterface = _interface.Match(line);
                    Match mComposition = composition.Match(line);


                    if (cp.ParseStart(line, (str) =>
                    {
                        diagram.Title = line[9..].Trim();
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
                        diagram.Title = str;

                    }))
                    {
                        continue;
                    }

                    if (cp.CommonParsing(line, (str) =>
                    {
                        currentPackage.Children.Add(new UMLOther(line));
                    },
           (str, alias) =>
           {
               currentPackage.Children.Add(new UMLNote(line, alias));
           },
           (str) =>
           {
               currentPackage.Children.Add(new UMLOther(line));
           },
           (str) =>
           {
               currentPackage.Children.Add(new UMLComment(line));
           },
           (str) =>
           {
               currentPackage.Children.Add(new UMLOther(line));
           }
           ))
                    {
                        continue;
                    }

                    if (mPorts.Success)
                    {
                       
                        if (currentComponent is not null)
                        {
                            if (mPorts.Groups[1].Value == "port ")
                            {
                                currentComponent.Ports.Add(mPorts.Groups["name"].Value);
                            }
                            else if (mPorts.Groups[1].Value == "portin ")
                            {
                                currentComponent.PortsIn.Add(mPorts.Groups["name"].Value);
                            }
                            else if (mPorts.Groups[1].Value == "portout ")
                            {
                                currentComponent.PortsOut.Add(mPorts.Groups["name"].Value);
                            }
                        }
                        continue;
                    }

                    if (line.StartsWith("participant", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    UMLDataType? DataType = null;

                    if (line == "}" && brackets.Count > 0 && brackets.Peek() == PACKAGENAME)
                    {
                        _ = brackets.Pop();
                        _ = packages.Pop();
                        _ = packagesStack.Pop();
                        currentPackage = packagesStack.First();
                        continue;
                    }


                    if (line == "}" && brackets.Count > 0 && brackets.Peek() == CLASSNAME)
                    {
                        _ = brackets.Pop();
                        _ = components.Pop();
                        currentComponent = components.FirstOrDefault();
                        continue;
                    }

                    if (line.EndsWith("{", StringComparison.Ordinal) && mPackage1.Success)
                    {
                        Match? s = mPackage1;

                        currentPackage = AddPackage(packages, aliases, brackets,
                            packagesStack, diagram, currentPackage, s);

                        continue;
                    }
                    else if (line.EndsWith("{", StringComparison.Ordinal) && mPackage2.Success)
                    {
                        Match? s = mPackage2;

                        currentPackage = AddPackage(packages, aliases, brackets,
                       packagesStack, diagram, currentPackage, s);



                        continue;
                    }
                    else if (mComponentBracket.Success)
                    {
                     
                        string package = GetPackage(packages);
                        DataType = new UMLComponent(package, Clean(mComponentBracket.Groups["name"].ValueSpan).ToString(),
                            mComponentBracket.Groups["alias"].Value);
                        if (!aliases.TryAdd(mComponentBracket.Groups["alias"].Value, DataType))
                        {
                            diagram.AddLineError($"Duplicate identifier : {mComponentBracket.Groups["alias"].Value}", linenumber);
                        }

                        if (currentComponent is not null)
                        {
                            currentComponent.Children.Add(DataType);
                        }

                        if (line.EndsWith("{", StringComparison.Ordinal))
                        {
                            brackets.Push(CLASSNAME);
                            components.Push((UMLComponent)DataType);
                            currentComponent = (UMLComponent)DataType;
                        }
                    }
                    else if (mComponentType.Success)
                    {
                        var parseResult = ParseComponentLine(line);
                        if (!parseResult.success || string.IsNullOrEmpty(parseResult.name))
                        {
                            continue;
                        }

                        string package = GetPackage(packages);

                        DataType = new UMLComponent(package, parseResult.name, parseResult.alias);

                        if (!aliases.TryAdd(parseResult.alias, DataType))
                        {
                            diagram.AddLineError($"Duplicate identifier : {parseResult.alias}", linenumber);

                        }

                        if (currentComponent is not null)
                        {
                            currentComponent.Children.Add(DataType);
                        }

                        if (line.EndsWith("{", StringComparison.Ordinal))
                        {
                            brackets.Push(CLASSNAME);
                            components.Push((UMLComponent)DataType);
                            currentComponent = (UMLComponent)DataType;
                        }
                    }
                    else if (mInterface.Success)
                    {
                       
                        string package = GetPackage(packages);
                        if (line.Length > 8)
                        {
                            var alias = string.IsNullOrWhiteSpace(mInterface.Groups["alias"].Value) 
                                ? mInterface.Groups[PACKAGENAME].Value : mInterface.Groups["alias"].Value;
                            DataType = new UMLInterface(package, Clean(mInterface.Groups[PACKAGENAME].ValueSpan).ToString(), alias,
                                new List<UMLDataType>());

                            if(!aliases.TryAdd(alias, DataType))
                            {
                                diagram.AddLineError($"Duplicate identifier : {alias}", linenumber);
                            }
                        }
                        if (line.EndsWith("{", StringComparison.Ordinal))
                        {
                            brackets.Push("interface");
                        }
                    }
                    else if (mComposition.Success)
                    {
                        try
                        {
                             

                            string left = mComposition.Groups["left"].Value.Trim();
                            string right = mComposition.Groups["right"].Value.Trim();


                            var leftComponent = TryGetComponent(left, diagram, packages, currentPackage, 
                                aliases, mComposition.Groups["leftbracket"].Length == 1, componentsMustBeDefined);
                            var rightComponent = TryGetComponent(right, diagram, packages, currentPackage, 
                                aliases, mComposition.Groups["rightbracket"].Length == 1, componentsMustBeDefined);


                            string arrow = mComposition.Groups["arrow"].Value.Trim();
                            if (leftComponent == null || rightComponent == null)
                            {
                                diagram.ExplainedErrors.Add(new UMLComponentDiagram.ExplainedError(line,
                                    linenumber, 
                                    $"(Left: {leftComponent?.Alias ?? "Not Found"}) (Right: {rightComponent?.Alias ?? "Not Found"})"));
                            }
                            else if (leftComponent is UMLComponent c)
                            {
                                if (rightComponent is UMLComponent rc)
                                {


                                    if (arrow.StartsWith("o", StringComparison.Ordinal))
                                    {
                                        rc.Exposes.Add(leftComponent);
                                    }
                                    else if (arrow.StartsWith("(", StringComparison.Ordinal))
                                    {
                                        rc.Consumes.Add(leftComponent);
                                    }
                                    else if (arrow.StartsWith("<", StringComparison.Ordinal))
                                    {
                                        rc.Consumes.Add(leftComponent);
                                    }
                                    else if (arrow.StartsWith("*", StringComparison.Ordinal))
                                    {
                                        rc.Consumes.Add(leftComponent);
                                    }
                                }
                                if (arrow.EndsWith("o", StringComparison.Ordinal))
                                {
                                    c.Exposes.Add(rightComponent);
                                }
                                else if (arrow.EndsWith("(", StringComparison.Ordinal))
                                {
                                    c.Consumes.Add(rightComponent);
                                }
                                else if (arrow.EndsWith(">", StringComparison.Ordinal))
                                {
                                    c.Consumes.Add(rightComponent);
                                }
                                else if (arrow.EndsWith("*", StringComparison.Ordinal))
                                {
                                    c.Consumes.Add(rightComponent);
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        diagram.AddLineError(line, linenumber);

                    }
                    if (DataType != null)
                    {
                        currentPackage.Children.Add(DataType);
                    }
                }
                catch (RegexMatchTimeoutException)
                {


                    diagram.ExplainedErrors.Add(new(line, linenumber, "Regex timeout"));
                }
                catch
                {
                    diagram.AddLineError(line, linenumber);

                }



            }

            return diagram;
        }

        private static UMLDataType? TryGetComponent(string left, UMLComponentDiagram diagram, 
            Stack<string> packages, UMLPackage currentPackage, 
            Dictionary<string, UMLDataType> aliases, bool bracketExists, bool componentsMustBeDefined)
        {
            UMLDataType? leftComponent = diagram.Entities.Find(p => p.Name == left && string.IsNullOrEmpty(p.Alias))
                              ?? diagram.ContainedPackages.Find(p => p.Name == left);
            if (leftComponent == null)
            {
                if (aliases.ContainsKey(left))
                {
                    leftComponent = aliases[left];
                }
                else if(componentsMustBeDefined)
                {
                    leftComponent = null;
                }
                else
                {
                    string package = GetPackage(packages);

                    if (left is not null && bracketExists)
                    {

             

                        leftComponent = new UMLComponent(package, Clean(left.AsSpan()).ToString(), left);
                        currentPackage.Children.Add(leftComponent);
                        _ = aliases.TryAdd(left, leftComponent);
                    }
                    else if (left is not null && !bracketExists)
                    {
                        leftComponent = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(z => z.PortsIn.Contains(left)
                          || z.PortsOut.Contains(left) || z.Ports.Contains(left));

                        if(leftComponent is null)
                        {
                            leftComponent = new UMLComponent(package, Clean(left.AsSpan()).ToString(), left);
                            currentPackage.Children.Add(leftComponent);
                            _ = aliases.TryAdd(left, leftComponent);
                        }

                    }
                    else
                    {


                        leftComponent = null;
                    }
                }
            }

            return leftComponent;
        }

        private static UMLPackage AddPackage(Stack<string> packages, Dictionary<string, UMLDataType> aliases, Stack<string> brackets, Stack<UMLPackage> packagesStack, UMLComponentDiagram d, UMLPackage currentPackage, Match s)
        {
            string name = Clean(s.Groups[PACKAGENAME].ValueSpan).ToString();
            packages.Push(name);
            brackets.Push(PACKAGENAME);

            string alias = string.IsNullOrWhiteSpace(s.Groups["alias"].Value) ? name : s.Groups["alias"].Value;

            UMLPackage? c = new UMLPackage(name,
                s.Groups["type"].Value, alias);

            currentPackage.Children.Add(c);

            d.ContainedPackages.Add(c);

            currentPackage = c;
            if (!aliases.TryAdd(alias, c))
            {
                d.AddLineError($"Duplicate identifier : {alias}", 0);
            }
            packagesStack.Push(c);
            return currentPackage;
        }

        public static async Task<UMLComponentDiagram?> ReadFile(string file, bool componentsMustBeDefined = true)
        {
            using StreamReader sr = new(file);
            UMLComponentDiagram? c = await ReadComponentDiagram(sr, file, componentsMustBeDefined);

            return c;
        }

        public static async Task<UMLComponentDiagram?> ReadString(string s, bool componentsMustBeDefined = true)
        {

            using StringReader sr = new(s);
            UMLComponentDiagram? c = await ReadComponentDiagram(sr, "", componentsMustBeDefined);

            return c;
        }
    }
}