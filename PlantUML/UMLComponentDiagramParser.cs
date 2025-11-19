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
    public class UMLComponentDiagramParser
    {
        private const string CLASSNAME = "class";
        private const string PACKAGENAME = "name";
        private static readonly Regex _component = new(@"^(?:component|database|queue|actor)\s*(?:""(?<name>[^""]+)""|(?<name>\w+))\s*(?:(?:<<(?<description>[^>]+)>>|\[(?<description>[^\]]+)\]|(?<description>(?:(?!\s+as\b|\s+#).)+?)(?=\s+(?:as\s+\w+|#|$))))?\s*(?:as\s+(?<alias>\w+))?\s*(?<color>#(?:[0-9A-Fa-f]{3,6}|[A-Za-z]+))?\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _interface = new(@"^(\(\)|interface)\s+\""*((?<name>[\w \\]+)\""*(\s+as\s+(?<alias>[\w]+))|(?<name>[\w \\]+)\""*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _packageRegex = new(@"^\s*(?<type>package|frame|node|cloud|node|folder|together|rectangle) +((?<name>[\w]+)|\""(?<name>[\w\s\W]+)\"")\s+as (?<alias>[\w\s]+)*\s+\{", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        private static readonly Regex _packageRegex2 = new(@"^\s*(?<type>package|frame|node|cloud|node|folder|together|rectangle) +\""*(?<name>[\w\W ]+)*\""*\s+\{", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        private static readonly Regex composition = new(@"^(?<leftbracket>\[*)(?<left>[\w ]+)\]* *(?<arrow>[\<\-\(\)o\[\]\=\,\.\#\<\|]+(?<direction>[\w\,\=\[\]]+)*[\->\(\)o\[\]\.\#\|\>]*) *(?<rightbracket>\[*)(?<right>[\w ]+)\]*", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        private static readonly Regex _ports = new(@"^\s*(port |portin |portout )(?<name>.*)", RegexOptions.Compiled);


        private static string Clean(string name)
        {
            string? t = name.Trim();
            return t.TrimEnd('{').Trim();
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

        private static async Task<UMLComponentDiagram?> ReadComponentDiagram(StreamReader sr, string fileName)
        {

            bool started = false;
            string? line = null;

            Stack<string> packages = new();

            Dictionary<string, UMLDataType> aliases = new();



            Stack<string> brackets = new();
            Regex removeGenerics = new("\\w+");
            Stack<UMLPackage> packagesStack = new();
            Stack<UMLComponent> components = new();
            UMLComponent? currentComponent = null;

            UMLPackage defaultPackage = new("");

            UMLComponentDiagram d = new(string.Empty, fileName, defaultPackage);
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

                    if (_ports.IsMatch(line))
                    {
                        Match? m = _ports.Match(line);
                        if (currentComponent is not null)
                        {
                            if (m.Groups[1].Value == "port ")
                            {
                                currentComponent.Ports.Add(m.Groups["name"].Value);
                            }
                            else if (m.Groups[1].Value == "portin ")
                            {
                                currentComponent.PortsIn.Add(m.Groups["name"].Value);
                            }
                            else if (m.Groups[1].Value == "portout ")
                            {
                                currentComponent.PortsOut.Add(m.Groups["name"].Value);
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

                    if (line.Trim().EndsWith("{", StringComparison.Ordinal) && _packageRegex.IsMatch(line))
                    {
                        Match? s = _packageRegex.Match(line);

                        currentPackage = AddPackage(packages, aliases, brackets,
                            packagesStack, d, currentPackage, s);

                        continue;
                    }
                    else if (line.Trim().EndsWith("{", StringComparison.Ordinal) && _packageRegex2.IsMatch(line))
                    {
                        Match? s = _packageRegex2.Match(line);

                        currentPackage = AddPackage(packages, aliases, brackets,
                       packagesStack, d, currentPackage, s);




                        continue;
                    }
                    else if (_component.IsMatch(line))
                    {
                        Match? g = _component.Match(line);
                        if (string.IsNullOrEmpty(g.Groups[PACKAGENAME].Value))
                        {
                            continue;
                        }

                        string package = GetPackage(packages);

                        DataType = new UMLComponent(package, Clean(g.Groups[PACKAGENAME].Value),
                            g.Groups["alias"].Value);

                        _ = aliases.TryAdd(g.Groups["alias"].Value, DataType);

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
                    else if (_interface.IsMatch(line))
                    {
                        Match? g = _interface.Match(line);
                        string package = GetPackage(packages);
                        if (line.Length > 8)
                        {
                            DataType = new UMLInterface(package, Clean(g.Groups[PACKAGENAME].Value), g.Groups["alias"].Value,
                                new List<UMLDataType>());
                            _ = aliases.TryAdd(g.Groups["alias"].Value, DataType);
                        }
                        if (line.EndsWith("{", StringComparison.Ordinal))
                        {
                            brackets.Push("interface");
                        }
                    }
                    else if (composition.IsMatch(line))
                    {
                        try
                        {
                            Match? m = composition.Match(line);

                            string left = m.Groups["left"].Value.Trim();
                            string right = m.Groups["right"].Value.Trim();

                            var leftComponent = TryGetComponent(left, d, packages, aliases, m.Groups["leftbracket"].Length == 1);
                            var rightComponent = TryGetComponent(right, d, packages, aliases, m.Groups["rightbracket"].Length == 1);


                            string arrow = m.Groups["arrow"].Value.Trim();
                            if (leftComponent == null || rightComponent == null)
                            {
                                d.ExplainedErrors.Add((line, linenumber, $"left: {leftComponent} right: {rightComponent}"));
                            }
                            else if (leftComponent is UMLComponent c)
                            {
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
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        d.AddLineError(line, linenumber);

                    }
                    if (DataType != null)
                    {
                        currentPackage.Children.Add(DataType);
                    }
                }
                catch (RegexMatchTimeoutException)
                {


                    d.ExplainedErrors.Add((line, linenumber, "Regex timeout"));
                }
                catch
                {
                    d.AddLineError(line, linenumber);

                }


            }

            return d;
        }

        private static UMLDataType? TryGetComponent(string left, UMLComponentDiagram d, Stack<string> packages, Dictionary<string, UMLDataType> aliases, bool bracketExists)
        {
            UMLDataType? leftComponent = d.Entities.Find(p => p.Name == left && string.IsNullOrEmpty(p.Alias))
                              ?? d.ContainedPackages.Find(p => p.Name == left);
            if (leftComponent == null)
            {
                if (aliases.ContainsKey(left))
                {
                    leftComponent = aliases[left];
                }
                else
                {
                    if (left is not null && bracketExists)
                    {

                        string package = GetPackage(packages);

                        leftComponent = new UMLComponent(package, Clean(left), left);
                        _ = aliases.TryAdd(left, leftComponent);
                    }
                    else if (left is not null && !bracketExists)
                    {
                        leftComponent = d.Entities.OfType<UMLComponent>().FirstOrDefault(z => z.PortsIn.Contains(left)
                          || z.PortsOut.Contains(left) || z.Ports.Contains(left));


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
            string name = Clean(s.Groups[PACKAGENAME].Value);
            packages.Push(name);
            brackets.Push(PACKAGENAME);

            UMLPackage? c = new UMLPackage(name,
                s.Groups["type"].Value, s.Groups["alias"].Value);

            currentPackage.Children.Add(c);

            d.ContainedPackages.Add(c);

            currentPackage = c;
            _ = aliases.TryAdd(string.IsNullOrWhiteSpace(s.Groups["alias"].Value) ? name : s.Groups["alias"].Value, c);
            packagesStack.Push(c);
            return currentPackage;
        }

        public static async Task<UMLComponentDiagram?> ReadFile(string file)
        {
            using StreamReader sr = new(file);
            UMLComponentDiagram? c = await ReadComponentDiagram(sr, file);

            return c;
        }

        public static async Task<UMLComponentDiagram?> ReadString(string s)
        {

            using MemoryStream ms = new(Encoding.UTF8.GetBytes(s));
            using StreamReader sr = new(ms);
            UMLComponentDiagram? c = await ReadComponentDiagram(sr, "");

            return c;
        }
    }
}