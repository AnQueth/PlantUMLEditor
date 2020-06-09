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

        private static Regex _component = new Regex("^(component |\\[)((?<name>[\\w \\\\]+)\\]|(?<name>[\\w\\\\]+))(\\s+as*\\s+)*(?<alias>\\w+)*", RegexOptions.Compiled);
        private static Regex _interface = new Regex("^(\\(\\)|interface)\\s+\\\"*((?<name>[\\w \\\\]+)\\\"*(\\s+as\\s+(?<alias>[\\w]+))|(?<name>[\\w \\\\]+)\\\"*)", RegexOptions.Compiled);

        private static Regex _packageRegex = new Regex("^\\s*(?<type>package|frame|node|cloud|database|node|folder) +\\\"*(?<name>[\\w ]+)*\\\"* *\\{", RegexOptions.Compiled);

        private static Regex composition = new Regex("^\\[*(?<left>[\\w ]+)\\]* *(?<arrow>[\\<\\-\\(\\)o]+(?<direction>[\\w]+)*[\\->\\(\\)o]+) *\\[*(?<right>[\\w ]+)\\]*", RegexOptions.Compiled);

        private static Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))", RegexOptions.Compiled);

        private static string Clean(string name)
        {
            var t = name.Trim();
            return t.TrimEnd('{').Trim();
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

        private static async Task<UMLComponentDiagram> ReadComponentDiagram(StreamReader sr, string fileName)
        {
            UMLComponentDiagram d = new UMLComponentDiagram(string.Empty, fileName);
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
                }

                if (line.StartsWith("end note"))
                    swallowingNotes = false;

                if (swallowingNotes)
                {
                    if (d.Entities.Last() is UMLNote n)
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

                    var c = new UMLPackage(Clean(s.Groups[PACKAGE].Value), s.Groups["type"].Value);
                    currentPackage.Children.Add(c);
                    currentPackage = c;

                    packagesStack.Push(c);

                    continue;
                }
                else if (_component.IsMatch(line))
                {
                    var g = _component.Match(line);
                    if (string.IsNullOrEmpty(g.Groups["name"].Value))
                        continue;

                    string package = GetPackage(packages);

                    DataType = new UMLComponent(package, Clean(g.Groups["name"].Value));

                    aliases.TryAdd(g.Groups["alias"].Value, DataType);

                    if (line.EndsWith("{"))
                    {
                        brackets.Push("class");
                    }
                }
                else if (_interface.IsMatch(line))
                {
                    var g = _interface.Match(line);
                    string package = GetPackage(packages);
                    if (line.Length > 8)
                        DataType = new UMLInterface(package, Clean(g.Groups["name"].Value));
                    aliases.TryAdd(g.Groups["alias"].Value, DataType);
                    if (line.EndsWith("{"))
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

                        var propType = d.Entities.Find(p => p.Name == left);
                        if (propType == null)
                            propType = aliases[left];

                        var fromType = d.Entities.Find(p => p.Name == right);
                        if (fromType == null)
                            fromType = aliases[right];

                        string arrow = m.Groups["arrow"].Value.Trim();

                        if (propType is UMLComponent c)
                        {
                            if (arrow.EndsWith("o"))
                            {
                                c.Exposes.Add(fromType);
                            }
                            else if (arrow.EndsWith("("))
                            {
                                c.Consumes.Add(fromType);
                            }
                            else if (arrow.EndsWith(">"))
                            {
                                c.Consumes.Add(fromType);
                            }
                        }
                    }
                    catch { }
                }
                if (DataType != null)
                    currentPackage.Children.Add(DataType);
            }

            return d;
        }

        public static async Task<UMLComponentDiagram> ReadFile(string file)
        {
            using (StreamReader sr = new StreamReader(file))
            {
                UMLComponentDiagram c = await ReadComponentDiagram(sr, file);

                return c;
            }
        }

        public static async Task<UMLComponentDiagram> ReadString(string s)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(s)))
            {
                using (StreamReader sr = new StreamReader(ms))
                {
                    UMLComponentDiagram c = await ReadComponentDiagram(sr, "");

                    return c;
                }
            }
        }
    }
}