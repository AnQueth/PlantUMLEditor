using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUML
{
    public class UMLSequenceDiagramParser : IPlantUMLParser
    {
        private static Regex _lifeLineRegex = new Regex("(?<type>participant|actor|control|component|database|boundary|entity|collections)\\s+\\\"*(?<name>[\\w\\.]+(\\s*\\<((?<generics>[\\s\\w]+)\\,*)*\\>)*)\\\"*(\\s+as (?<alias>[\\w]+))*", RegexOptions.Compiled);

        private static Regex lineConnectionRegex = new Regex("^([a-zA-Z0-9\\>\\<\\,]+|[\\-<>\\]\\[\\#]+)\\s*([a-zA-Z0-9\\-><\\\\/\\]\\[\\#]+)\\s*([a-zA-Z0-9\\-><]*)\\s*\\:*\\s*(.+)*$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
        private static Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))", RegexOptions.Compiled);

        private static string Clean(string name)
        {
            var t = name.Trim();
            return t.TrimEnd('{').Trim();
        }

        private static UMLSignature GetActionSignature(string actionSignature, ILookup<string, UMLDataType> types,
            UMLSequenceLifeline to, UMLSequenceConnection previous, UMLSequenceDiagram d)
        {
            UMLSignature action = null;

            if (actionSignature.StartsWith("\"") && actionSignature.EndsWith("\""))
            {
                action = new UMLCustomAction(actionSignature);
                return action;
            }

            if (to != null && to.DataTypeId != null)
            {
                foreach (var toType in types[to.DataTypeId])
                {
                    action = CheckActionOnType(toType, actionSignature);


                    if (action != null)
                    {
                        break;
                    }


                }
            }

            if (action == null)
            {
                if (actionSignature.StartsWith("<<create>>"))
                {
                    action = new UMLCreateAction(actionSignature);
                }
                else if (actionSignature.StartsWith("return"))
                {
                    if (previous != null && previous.Action != null)
                        action = new UMLReturnFromMethod(previous.Action);
                    else
                    {
                        string rest = actionSignature.Substring(6).Trim();

                        if (d.LifeLines.Find(p => p.Text == rest) != null)
                        {
                            action = new UMLLifelineReturnAction(actionSignature);
                        }
                    }
                }

                if (action == null)
                    action = new UMLUnknownAction(actionSignature);
            }

            return action;
        }

        private static UMLSignature CheckActionOnType(UMLDataType toType, string actionSignature)
        {
            UMLSignature action = toType.Methods.Find(p => p.Signature == actionSignature);
            if (action == null)
                action = toType.Properties.Find(p => p.Signature == actionSignature);
            if (action != null)
                return action;


            foreach (var item in toType.Bases)
            {


                action = CheckActionOnType(item, actionSignature);
                if (action != null)
                    break;
            }
            return action;
        }

        private static async Task<UMLSequenceDiagram> ReadDiagram(StreamReader sr, List<UMLClassDiagram> classDiagrams, string fileName, bool justLifeLines)
        {
            var types = classDiagrams.SelectMany(p => p.DataTypes).Where(p => p is UMLClass || p is UMLInterface).ToLookup(p => p.Name);

            UMLSequenceDiagram d = new UMLSequenceDiagram(string.Empty, fileName);
            bool started = false;
            string line = null;

            Stack<UMLSequenceBlockSection> activeBlocks = new Stack<UMLSequenceBlockSection>();

            int lineNumber = 0;
            UMLSequenceConnection previous = null;

            bool swallowingNotes = false;

            while ((line = await sr.ReadLineAsync()) != null)
            {
                lineNumber++;
                line = line.Trim();

                if (line == "@startuml")
                {
                    started = true;
                    continue;
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
                        continue;
                }

                if (line.StartsWith("end note", StringComparison.Ordinal))
                    swallowingNotes = false;

                if (swallowingNotes)
                    continue;

                if (line.StartsWith("class", StringComparison.Ordinal) || line.StartsWith("interface", StringComparison.Ordinal) || line.StartsWith("package", StringComparison.Ordinal))
                    return null;

                if (line.StartsWith("title", StringComparison.Ordinal))
                {
                    d.Title = line.Substring(5).Trim();
                    continue;
                }

                if (TryParseLifeline(line, types, out var lifeline))
                {
                    d.LifeLines.Add(lifeline);
                    lifeline.LineNumber = lineNumber;

                    continue;
                }

                if (!justLifeLines)
                {
                    UMLSequenceBlockSection sectionBlock = null;

                    if (TryParseAllConnections(line, d, types, previous, out UMLSequenceConnection connection))
                    {
                        connection.LineNumber = lineNumber;

                        if (activeBlocks.Count == 0)
                            d.Entities.Add(connection);
                        else
                            activeBlocks.Peek().Entities.Add(connection);

                        previous = connection;
                    }
                    else if ((sectionBlock = UMLSequenceBlockSection.TryParse(line)) != null)
                    {
                        sectionBlock.LineNumber = lineNumber;



                        if (activeBlocks.Count == 0)
                        {
                            d.Entities.Add(sectionBlock);
                        }
                        else
                        {
                            activeBlocks.Peek().Entities.Add(sectionBlock);
                        }
                        activeBlocks.Push(sectionBlock);
                    }
                    else if (activeBlocks.Count != 0 && activeBlocks.Peek().IsEnding(line))
                    {

                        activeBlocks.Pop();
                        if (activeBlocks.Count > 0)
                        {
                            var p = activeBlocks.Peek().SectionType;
                            if (p == UMLSequenceBlockSection.SectionTypes.If
                                || p == UMLSequenceBlockSection.SectionTypes.Parrallel
                                || p == UMLSequenceBlockSection.SectionTypes.Try
                                )
                                activeBlocks.Pop();
                        }
                    }
                }
            }

            return d;
        }

        private static bool TryParseConnection(string fromAlias, string arrow, string toAlias,
            string actionSignature, UMLSequenceDiagram d, ILookup<string, UMLDataType> types, UMLSequenceConnection previous,
            out UMLSequenceConnection connection)
        {
            connection = null;

            if (!arrow.Contains("->"))
                return false;

            var from = d.LifeLines.Find(p => p.Alias == fromAlias);

            UMLSignature action = null;
            var to = d.LifeLines.Find(p => p.Alias == toAlias);
            if (to != null)
            {
                bool isCreate = arrow == "-->";
                action = GetActionSignature(actionSignature, types, to, previous, d);
            }
            connection = new UMLSequenceConnection()
            {
                ToShouldBeUsed = true,
                From = from,
                To = to,
                Action = action,
                ToName = toAlias,
                FromShouldBeUsed = true,
                FromName = fromAlias
            };

            return true;
        }

        private static bool TryParseReturnToEmpty(string fromAlias, string arrow,
            string actionSignature, UMLSequenceDiagram d, ILookup<string, UMLDataType> types, UMLSequenceConnection previous,
            out UMLSequenceConnection connection)
        {
            if (arrow.StartsWith("<"))
            {
                UMLSignature method = GetActionSignature(actionSignature, types, null, previous, d);

                var ft = d.LifeLines.Find(p => p.Alias == fromAlias);

                connection = new UMLSequenceConnection()
                {
                    From = ft,
                    Action = method
                };

                return true;
            }

            connection = null;

            return false;
        }

        private static bool TypeParseConnectionFromEmpty(string arrow, string toAlias,
            string actionSignature, UMLSequenceDiagram d,
            ILookup<string, UMLDataType> types, UMLSequenceConnection previous, out UMLSequenceConnection connection)
        {
            if (arrow.StartsWith("-"))
            {
                bool isCreate = arrow == "-->";

                var to = d.LifeLines.Find(p => p.Alias == toAlias);

                UMLSignature action = null;
                if (to != null && to.DataTypeId != null && types.Contains(to.DataTypeId))
                {
                    action = GetActionSignature(actionSignature, types, to, previous, d);
                }

                connection = new UMLSequenceConnection()
                {
                    ToShouldBeUsed = true,
                    To = to,
                    Action = action,
                    ToName = toAlias
                };

                return true;
            }

            connection = null;
            return false;
        }

        public static async Task<UMLSequenceDiagram> ReadFile(string file, List<UMLClassDiagram> types, bool justLifeLines)
        {
            using (StreamReader sr = new StreamReader(file))
            {
                UMLSequenceDiagram c = await ReadDiagram(sr, types, file, justLifeLines);

                return c;
            }
        }

        public static async Task<UMLSequenceDiagram> ReadString(string s, List<UMLClassDiagram> types, bool justLifeLines)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(s)))
            {
                using (StreamReader sr = new StreamReader(ms))
                {
                    UMLSequenceDiagram c = await ReadDiagram(sr, types, "", justLifeLines);

                    return c;
                }
            }
        }

        public static bool TryParseAllConnections(string line, UMLSequenceDiagram diagram,
            ILookup<string, UMLDataType> types, UMLSequenceConnection previous, out UMLSequenceConnection connection)
        {
            connection = null;

            try
            {
                var m = lineConnectionRegex.Match(line);
                if (!m.Success)
                {
                    return false;
                }

                string fromAlias = m.Groups[1].Value[0] != '<' && m.Groups[1].Value[0] != '-' && m.Groups[1].Value[0] != '>' ? m.Groups[1].Value : null;
                string arrow = fromAlias == null ? m.Groups[1].Value : m.Groups[2].Value;
                string toAlias = fromAlias == null ? (m.Groups[2].Value == "-" ? m.Groups[3].Value : m.Groups[2].Value) : m.Groups[3].Value;

                int l = line.IndexOf(':');

                string actionSignature = l != -1 ? line.Substring(l).Trim(':').Trim() : string.Empty;

                if (fromAlias == null)
                {
                    if (arrow.StartsWith("<"))
                    {
                        if (TryParseReturnToEmpty(toAlias, arrow, actionSignature, diagram, types, previous, out connection))
                        {
                            return true;
                        }
                    }
                    else if (TypeParseConnectionFromEmpty(arrow, toAlias, actionSignature, diagram, types, previous, out connection))
                    {
                        return true;
                    }
                }
                else if (TryParseConnection(fromAlias, arrow, toAlias, actionSignature, diagram, types, previous, out connection))
                {
                    return !string.IsNullOrWhiteSpace(connection.ToName) && !string.IsNullOrWhiteSpace(connection.FromName);
                }
            }
            catch
            {
            }

            return false;
        }

        public static bool TryParseLifeline(string line, ILookup<string, UMLDataType> types, out UMLSequenceLifeline lifeline)
        {
            var m = _lifeLineRegex.Match(line);
            if (m.Success)
            {
                string type = m.Groups["type"].Value;
                string name = m.Groups["name"].Value;
                string alias = m.Groups["alias"].Value;
                if (string.IsNullOrEmpty(alias))
                    alias = name;

                if (!types.Contains(name))
                {
                    lifeline = new UMLSequenceLifeline(type, name, alias, null);
                }
                else
                    lifeline = new UMLSequenceLifeline(type, name, alias, types[name].First().Id);
                return true;
            }
            lifeline = null;
            return false;
        }
    }
}