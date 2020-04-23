using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUML
{
    public class UMLSequenceDiagramParser
    {
        public static async Task<UMLSequenceDiagram> ReadString(string s, Dictionary<string, UMLDataType> types, bool justLifeLines)
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

        public static async Task<UMLSequenceDiagram> ReadDiagram(string file, Dictionary<string, UMLDataType> types, bool justLifeLines)
        {
            using (StreamReader sr = new StreamReader(file))
            {
                UMLSequenceDiagram c = await ReadDiagram(sr, types, file, justLifeLines);

                return c;
            }
        }

        public static bool TryParseLifeline(string line, Dictionary<string, UMLDataType> types, out UMLSequenceLifeline lifeline)
        {
            if (line.StartsWith("participant ") || line.StartsWith("actor "))
            {
                var items = line.Split(new string[] { " ", "as" }, StringSplitOptions.RemoveEmptyEntries);
                if (items.Length == 3)
                {
                    if (!types.ContainsKey(items[1]))
                    {
                        lifeline = new UMLSequenceLifeline(items[1], items[2], null);
                    }
                    else
                        lifeline = new UMLSequenceLifeline(items[1], items[2], types[items[1]].Id);
                    return true;
                }
            }
            lifeline = null;
            return false;
        }

        private static async Task<UMLSequenceDiagram> ReadDiagram(StreamReader sr, Dictionary<string, UMLDataType> types, string fileName, bool justLifeLines)
        {
            UMLSequenceDiagram d = new UMLSequenceDiagram(string.Empty, fileName);
            bool started = false;
            string line = null;

            Dictionary<string, string> aliases = new Dictionary<string, string>();

            Stack<UMLSequenceBlockSection> activeBlocks = new Stack<UMLSequenceBlockSection>();

            int lineNumber = 0;
            UMLSequenceConnection previous = null;
            Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))");

            bool swallowingNotes = false;

            while ((line = await sr.ReadLineAsync()) != null)
            {
                lineNumber++;
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


                if (line.StartsWith("class") || line.StartsWith("interface") || line.StartsWith("package"))
                    return null;

                if (line.StartsWith("title"))
                {
                    d.Title = line.Substring(5).Trim();
                }

                if (TryParseLifeline(line, types, out var lifeline))
                {
                    aliases.Add(lifeline.Alias, lifeline.DataTypeId);
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

                        if (sectionBlock.TakeOverOwnership)
                        {
                            activeBlocks.Pop();


                        }

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
                    }
                }
            }

            return d;
        }
        static Regex lineConnectionRegex = new Regex("([a-zA-Z0-9]+|[\\-<>]+) *([a-zA-Z0-9\\-><]+) *([a-zA-Z0-9\\-><:]*) *(.+)");
        public static bool TryParseAllConnections(string line, UMLSequenceDiagram diagram,
            Dictionary<string, UMLDataType> types, UMLSequenceConnection previous, out UMLSequenceConnection connection)
        {
            connection = null;



            var m = lineConnectionRegex.Match(line);



            try
            {
                string fromAlias = m.Groups[1].Value[0] != '<' && m.Groups[1].Value[0] != '-' && m.Groups[1].Value[0] != '>' ? m.Groups[1].Value : null;
                string arrow = fromAlias == null ? m.Groups[1].Value : m.Groups[2].Value;
                string toAlias = fromAlias == null ? m.Groups[2].Value : m.Groups[3].Value;

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
                    else if (TypeParseConnectionFromEmpty(arrow, toAlias, actionSignature, diagram, types, out connection))
                    {
                        return true;
                    }
                }
                else if (TryParseConnection(fromAlias, arrow, toAlias, actionSignature, diagram, types, out connection))
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryParseConnection(string fromAlias, string arrow, string toAlias,
            string actionSignature, UMLSequenceDiagram d, Dictionary<string, UMLDataType> types,
            out UMLSequenceConnection connection)
        {
            var from = d.LifeLines.Find(p => p.Alias == fromAlias);
            if (from != null)
            {
                var to = d.LifeLines.Find(p => p.Alias == toAlias);
                if (to != null)
                {
                    bool isCreate = arrow == "-->";
                    UMLMethod action = null;
                    if (to.DataTypeId != null && types.ContainsKey(to.DataTypeId))
                    {



                        var toType = types[to.DataTypeId];
               
                        if (toType != null)
                        {
                            action = toType.Methods.Find(p => p.Signature == actionSignature);
                        }
                    }
                    if (action == null)
                        action = new UMLUnknownAction(actionSignature);

                    connection = new UMLSequenceConnection()
                    {
                        From = from,
                        To = to,
                        Action = action
                    };

                    return true;
                }
            }
            connection = null;
            return false;
        }

        private static bool TypeParseConnectionFromEmpty(string arrow, string toAlias,
            string actionSignature, UMLSequenceDiagram d,
            Dictionary<string, UMLDataType> types, out UMLSequenceConnection connection)
        {
            if (arrow.StartsWith("-"))
            {
                bool isCreate = arrow == "-->";

                var to = d.LifeLines.Find(p => p.Alias == toAlias);

                UMLMethod action = null;
                if (types.ContainsKey(to.DataTypeId))
                {



                    var toType = types[to.DataTypeId];

                    if (toType != null)
                    {
                        action = toType.Methods.Find(p => p.Signature == actionSignature);
                    }
                }
                if (action == null)
                    action = new UMLUnknownAction(actionSignature);


                connection = new UMLSequenceConnection()
                {
                    To = to,
                    Action = action
                };

                return true;
            }

            connection = null;
            return false;
        }

        private static bool TryParseReturnToEmpty(string fromAlias, string arrow,
            string actionSignature, UMLSequenceDiagram d, Dictionary<string, UMLDataType> types, UMLSequenceConnection previous,
            out UMLSequenceConnection connection)
        {
            if (arrow.StartsWith("<"))
            {
                UMLMethod method = new UMLUnknownAction(actionSignature);

                var ft = d.LifeLines.Find(p => p.Alias == fromAlias);

                if (actionSignature.StartsWith("return"))
                {
                    method = new UMLReturnFromMethod(previous?.Action);
                }

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

        private static string Clean(string name)
        {
            var t = name.Trim();
            return t.TrimEnd('{').Trim();
        }
    }
}