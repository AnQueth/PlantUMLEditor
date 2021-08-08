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
        private const string PACKAGE = "name";

        private static readonly Regex _component = new(@"^(?:(?:component |database |queue |actor ) *(?:(?<name>[\w]+)|(?:(?:\[|\"")(?<name>[^\""]+)(?:\]|\"")))) *(?:\[(?<description>[\s\w]+)\])*(?: *as +(?<alias>[\w]+))* *(?<color>#[\w]+)*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _interface = new(@"^(\(\)|interface)\s+\""*((?<name>[\w \\]+)\""*(\s+as\s+(?<alias>[\w]+))|(?<name>[\w \\]+)\""*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _packageRegex = new(@"^\s*(?<type>package|frame|node|cloud|database|node|folder|together|rectangle) +((?<name>[\w]+)|\""(?<name>[\w\s]+)\"")\s+as (?<alias>[\w\s]+)*\s+\{", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        private static readonly Regex _packageRegex2 = new(@"^\s*(?<type>package|frame|node|cloud|database|node|folder|together|rectangle) +\""*(?<name>[\w ]+)*\""*\s+\{", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        private static readonly Regex _title = new("^title (?<title>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex composition = new(@"^\[*(?<left>[\w ]+)\]* *(?<arrow>[\<\-\(\)o\[\]\#]+(?<direction>[\w]+)*[\->\(\)o\[\]\#]+) *\[*(?<right>[\w ]+)\]*", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));

        private static readonly Regex legend = new("legend", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex notes = new(@"note *((?<sl>(?<placement>\w+) of (?<target>\w+) *: *(?<text>.*))|(?<sl>(?<placement>\w+) *: *(?<text>.*))|(?<sl>\""(?<text>[\w\W]+)\"" as (?<alias>\w+))|(?<placement>\w+) of (?<target>\w+)| as (?<alias>\w+))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string Clean(string name)
        {
            var t = name.Trim();
            return t.TrimEnd('{').Trim();
        }

        private static string GetPackage(Stack<string> packages)
        {
            StringBuilder sb = new();
            int x = 0;
            foreach (var item in packages.Reverse())
            {
                _ = sb.Append(item);
                if (x < packages.Count - 1)
                    _ = sb.Append('.');
                x++;
            }

            return sb.ToString();
        }

        private static async Task<UMLComponentDiagram?> ReadComponentDiagram(StreamReader sr, string fileName)
        {
            UMLComponentDiagram d = new(string.Empty, fileName);
            bool started = false;
            string? line = null;

            Stack<string> packages = new();

            Dictionary<string, UMLDataType> aliases = new();

            bool swallowingNotes = false;

            Stack<string> brackets = new();
            Regex removeGenerics = new("\\w+");
            Stack<UMLPackage> packagesStack = new();

            UMLPackage defaultPackage = new("");
            d.Package = defaultPackage;
            packagesStack.Push(defaultPackage);
            var currentPackage = defaultPackage;

            int linenumber = 0;

            while ((line = await sr.ReadLineAsync()) != null)
            {
                linenumber++;
                line = line.Trim();

                try
                {

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
                        if (!swallowingNotes)
                            continue;
                    }

                    if (line.StartsWith("end note", StringComparison.InvariantCulture))
                        swallowingNotes = false;

                    if (swallowingNotes)
                    {
                        if (d.Entities.Last() is UMLNote n)
                        {
                            n.Text += "\r\n" + line;
                        }
                        continue;
                    }
                    if (line.StartsWith("participant", StringComparison.InvariantCulture))
                        return null;

                    UMLDataType? DataType = null;

                    if (line == "}" && brackets.Count > 0 && brackets.Peek() == PACKAGE)
                    {
                        _ = brackets.Pop();
                        _ = packages.Pop();
                        _ = packagesStack.Pop();
                        currentPackage = packagesStack.First();
                    }

                    if (line.StartsWith("title", StringComparison.InvariantCulture))
                    {
                        if (line.Length > 6)
                            d.Title = line[6..];
                        continue;
                    }
                    else if (line.Trim().EndsWith("{", StringComparison.InvariantCulture) && _packageRegex.IsMatch(line))
                    {
                        var s = _packageRegex.Match(line);

                        packages.Push(Clean(s.Groups[PACKAGE].Value));
                        brackets.Push(PACKAGE);

                        var c = new UMLPackage(Clean(s.Groups[PACKAGE].Value), s.Groups["type"].Value);
                        currentPackage.Children.Add(c);

                        d.ContainedPackages.Add(c);

                        currentPackage = c;
                        _ = aliases.TryAdd(Clean(s.Groups[PACKAGE].Value), c);
                        packagesStack.Push(c);

                        continue;
                    }
                    else if (line.Trim().EndsWith("{", StringComparison.InvariantCulture) && _packageRegex2.IsMatch(line))
                    {
                        var s = _packageRegex2.Match(line);

                        packages.Push(Clean(s.Groups[PACKAGE].Value));
                        brackets.Push(PACKAGE);

                        var c = new UMLPackage(Clean(s.Groups[PACKAGE].Value), s.Groups["type"].Value);
                        d.ContainedPackages.Add(c);
                        currentPackage.Children.Add(c);
                        currentPackage = c;
                        _ = aliases.TryAdd(Clean(s.Groups[PACKAGE].Value), c);
                        packagesStack.Push(c);

                        continue;
                    }
                    else if (_component.IsMatch(line))
                    {
                        var g = _component.Match(line);
                        if (string.IsNullOrEmpty(g.Groups["name"].Value))
                            continue;

                        string package = GetPackage(packages);

                        DataType = new UMLComponent(package, Clean(g.Groups["name"].Value), g.Groups["alias"].Value);

                        _ = aliases.TryAdd(g.Groups["alias"].Value, DataType);

                        if (line.EndsWith("{", StringComparison.InvariantCulture))
                        {
                            brackets.Push("class");
                        }
                    }
                    else if (_interface.IsMatch(line))
                    {
                        var g = _interface.Match(line);
                        string package = GetPackage(packages);
                        if (line.Length > 8)
                        {
                            DataType = new UMLInterface(package, Clean(g.Groups["name"].Value), new List<UMLDataType>());
                            _ = aliases.TryAdd(g.Groups["alias"].Value, DataType);
                        }
                        if (line.EndsWith("{", StringComparison.InvariantCulture))
                        {
                            brackets.Push("interface");
                        }
                    }
                    else if (composition.IsMatch(line))
                    {
                        try
                        {
                            var m = composition.Match(line);

                            string left = m.Groups["left"].Value.Trim();
                            string right = m.Groups["right"].Value.Trim();

                            var leftComponent = d.Entities.Find(p => p.Name == left)
                                ?? d.ContainedPackages.Find(p => p.Name == left);
                            if (leftComponent == null)
                            {
                                if (aliases.ContainsKey(left))
                                    leftComponent = aliases[left];
                                else
                                    leftComponent = null;
                            }
                            var fromType = d.Entities.Find(p => p.Name == right)
                                 ?? d.ContainedPackages.Find(p => p.Name == right);
                            if (fromType == null)
                            {
                                if (aliases.ContainsKey(right))
                                    fromType = aliases[right];
                                else
                                    fromType = null;
                            }
                            string arrow = m.Groups["arrow"].Value.Trim();
                            if (leftComponent == null || fromType == null)
                            {
                                d.Errors.Add((line, linenumber, $"{leftComponent} {fromType}"));
                            }
                            else if (leftComponent is UMLComponent c)
                            {
                                if (arrow.EndsWith("o", StringComparison.InvariantCulture))
                                {
                                    c.Exposes.Add(fromType);
                                }
                                else if (arrow.EndsWith("(", StringComparison.InvariantCulture))
                                {
                                    c.Consumes.Add(fromType);
                                }
                                else if (arrow.EndsWith(">", StringComparison.InvariantCulture))
                                {
                                    c.Consumes.Add(fromType);
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        //d.Errors.Add((line, linenumber, "Not Processed"));
                    }
                    if (DataType != null)
                        currentPackage.Children.Add(DataType);
                }
                catch (RegexMatchTimeoutException  )
                {
                    d.Errors.Add(( line, linenumber, "Regex timeout"));
                }

            
            }

            return d;
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