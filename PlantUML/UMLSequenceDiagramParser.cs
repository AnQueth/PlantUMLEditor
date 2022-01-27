using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UMLModels;

namespace PlantUML
{
    public class UMLSequenceDiagramParser : IPlantUMLParser
    {
        private static readonly Regex _lifeLineRegex = new(@"^(?<type>participant|actor|control|component|database|boundary|entity|collections)\s+\""*(?<name>[\w \.]+(\s*\<((?<generics>[\s\w]+)\,*)*\>)*)\""*(\s+as (?<alias>[\w]+))*$", RegexOptions.Compiled);

        private static readonly Regex lineConnectionRegex = new("^([a-zA-Z0-9\\>\\<\\,]+|[\\-<>\\]\\[\\#]+)\\s*([a-zA-Z0-9\\-><\\\\/\\]\\[\\#]+)\\s*([a-zA-Z0-9\\-><]*)\\s*\\:*\\s*(.+)*$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
        private static readonly Regex notes = new("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))", RegexOptions.Compiled);

        private static UMLSignature? CheckActionOnType(UMLDataType toType, string actionSignature)
        {
            UMLSignature? action = toType.Methods.Find(p => p.Signature == actionSignature);
            if (action == null)
                action = toType.Properties.Find(p => p.Signature == actionSignature);
            if (action != null)
                return action;

            foreach (var item in toType.Bases)
            {
                action = CheckActionOnType(item, actionSignature);
                if (action != null)
                    return action;
            }
            return null;
        }

        private static string Clean(string name)
        {
            var t = name.Trim();
            return t.TrimEnd('{').Trim();
        }

        private static UMLSignature GetActionSignature(string actionSignature, ILookup<string, UMLDataType> types,
            UMLSequenceLifeline? to, UMLSequenceConnection? previous, UMLSequenceDiagram d)
        {
            UMLSignature? action = null;

            if (actionSignature.StartsWith("\"", StringComparison.Ordinal) && actionSignature.EndsWith("\"", StringComparison.Ordinal))
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
                if (actionSignature.StartsWith("<<create>>", StringComparison.Ordinal))
                {
                    action = new UMLCreateAction(actionSignature);
                }
                else if (actionSignature.StartsWith("return", StringComparison.Ordinal))
                {
                    if (previous != null && previous.Action != null)
                        action = new UMLReturnFromMethod(previous.Action);
                    else
                    {
                        string rest = actionSignature[6..].Trim();

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

        private static async Task<UMLSequenceDiagram?> ReadDiagram(StreamReader sr, LockedList<UMLClassDiagram> classDiagrams, string fileName, bool justLifeLines)
        {
            var types = classDiagrams.SelectMany(p => p.DataTypes).Where(p => p is UMLClass or UMLInterface).ToLookup(p => p.Name);

            UMLSequenceDiagram d = new(string.Empty, fileName);
            bool started = false;
            string? line = null;

            Stack<UMLSequenceBlockSection> activeBlocks = new();

            int lineNumber = 0;
            UMLSequenceConnection? previous = null;

            bool swallowingNotes = false;

            while ((line = await sr.ReadLineAsync()) != null)
            {
                lineNumber++;
                line = line.Trim();

                if (line.StartsWith( "@startuml", StringComparison.Ordinal))
                {
                    if(line.Length > 9)
                    {
                        d.Title = line[9..].Trim();
                    }
                    started = true;
                    continue;
                }

                if (line == "'@@novalidate")
                {
                    d.ValidateAgainstClasses = false;
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
                    d.Title = line[5..].Trim();
                    continue;
                }

                if (TryParseLifeline(line, types, lineNumber, out UMLSequenceLifeline? lifeline))
                {
                    d.LifeLines.Add(lifeline);
                   

                    continue;
                }

                if (!justLifeLines)
                {
                    

                    if (TryParseAllConnections(line, d, types, previous, lineNumber, out UMLSequenceConnection ? connection))
                    {
                        

                        if (activeBlocks.Count == 0)
                            d.Entities.Add(connection);
                        else
                            activeBlocks.Peek().Entities.Add(connection);

                        previous = connection;
                    }
                    else if ( UMLSequenceBlockSection.TryParse(line, lineNumber, out var sectionBlock) )
                    {
                         

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
                    else if (activeBlocks.Count != 0 && line.StartsWith("end", StringComparison.Ordinal))
                    {
                        _ = activeBlocks.Pop();
                        if (activeBlocks.Count > 0)
                        {
                            var p = activeBlocks.Peek().SectionType;
                            if (p is UMLSequenceBlockSection.SectionTypes.If
                                or UMLSequenceBlockSection.SectionTypes.Parrallel
                                or UMLSequenceBlockSection.SectionTypes.Try
                                )
                                _ = activeBlocks.Pop();
                        }
                    }
                }
            }

            return d;
        }

        private static bool TryParseConnection(string fromAlias, string arrow, string toAlias,
            string actionSignature, UMLSequenceDiagram d, ILookup<string, UMLDataType> types, UMLSequenceConnection? previous,
                int lineNumber,
            [NotNullWhen(true)] out UMLSequenceConnection? connection)
        {
            connection = null;

            if (!arrow.Contains("->", StringComparison.Ordinal))
                return false;

            var from = d.LifeLines.Find(p => p.Alias == fromAlias);

            UMLSignature? action = null;
            var to = d.LifeLines.Find(p => p.Alias == toAlias);
            if (to != null)
            {
                bool isCreate = arrow == "-->";
                action = GetActionSignature(actionSignature, types, to, previous, d);
            }
            connection = new UMLSequenceConnection(from, to, action, fromAlias, toAlias, true, true, lineNumber);
            

            return true;
        }

        private static bool TryParseReturnToEmpty(string fromAlias, string arrow,
            string actionSignature, UMLSequenceDiagram d, ILookup<string, UMLDataType> types, UMLSequenceConnection? previous,
            int lineNumber,
           [NotNullWhen(true)] out UMLSequenceConnection? connection)
        {
            if (arrow.StartsWith("<", StringComparison.Ordinal))
            {
                UMLSignature method = GetActionSignature(actionSignature, types, null, previous, d);

                var ft = d.LifeLines.Find(p => p.Alias == fromAlias);
                if(ft == null)
                {
                    Debug.WriteLine($"{fromAlias} not found");
                    connection = null;
                    return false;
                }
                connection = new UMLSequenceConnection(ft, method, lineNumber);
                

                return true;
            }

            connection = null;

            return false;
        }

        private static bool TypeParseConnectionFromEmpty(string arrow, string toAlias,
            string actionSignature, UMLSequenceDiagram d,
            ILookup<string, UMLDataType> types, UMLSequenceConnection? previous, int lineNumber,
            [NotNullWhen(true)] out UMLSequenceConnection? connection)
        {
            if (arrow.StartsWith("-", StringComparison.Ordinal))
            {
                bool isCreate = arrow == "-->";

                var to = d.LifeLines.Find(p => p.Alias == toAlias);

                UMLSignature? action = null;
                if (to != null && to.DataTypeId != null && types.Contains(to.DataTypeId))
                {
                    action = GetActionSignature(actionSignature, types, to, previous, d);
                }

                connection = new UMLSequenceConnection( to, true, action, toAlias, lineNumber) ;

                return true;
            }

            connection = null;
            return false;
        }

        public static async Task<UMLSequenceDiagram?> ReadFile(string file, LockedList<UMLClassDiagram> types, bool justLifeLines)
        {
            using StreamReader sr = new(file);
            UMLSequenceDiagram? c = await ReadDiagram(sr, types, file, justLifeLines);

            return c;
        }

        public static async Task<UMLSequenceDiagram?> ReadString(string s, LockedList<UMLClassDiagram> types, bool justLifeLines)
        {
            using MemoryStream ms = new(Encoding.UTF8.GetBytes(s));
            using StreamReader sr = new(ms);
            UMLSequenceDiagram? c = await ReadDiagram(sr, types, "", justLifeLines);

            return c;
        }

        public static bool TryParseAllConnections(string line, UMLSequenceDiagram diagram,
            ILookup<string, UMLDataType> types, UMLSequenceConnection? previous, int lineNumber,
            [NotNullWhen(true)] out UMLSequenceConnection? connection)
        {
            connection = null;

            try
            {
                var m = lineConnectionRegex.Match(line);
                if (!m.Success)
                {
                    return false;
                }

                string? fromAlias = m.Groups[1].Value[0] is not '<' and not '-' and not '>' ? m.Groups[1].Value : null;
                string arrow = fromAlias == null ? m.Groups[1].Value : m.Groups[2].Value;
                string toAlias = fromAlias == null ? (m.Groups[2].Value == "-" ? m.Groups[3].Value : m.Groups[2].Value) : m.Groups[3].Value;

                int l = line.IndexOf(':');

                string actionSignature = l != -1 ? line[l..].Trim(':').Trim() : string.Empty;

                if (fromAlias == null)
                {
                    if (arrow.StartsWith("<", StringComparison.Ordinal))
                    {
                        if (TryParseReturnToEmpty(toAlias, arrow, actionSignature, diagram, types, previous, lineNumber, out connection))
                        {
                            return true;
                        }
                    }
                    else if (TypeParseConnectionFromEmpty(arrow, toAlias, actionSignature, diagram, types, previous, lineNumber, out connection))
                    {
                        return true;
                    }
                }
                else if (TryParseConnection(fromAlias, arrow, toAlias, actionSignature, diagram, types, previous, lineNumber, out connection))
                {
                    return !string.IsNullOrWhiteSpace(connection.ToName) && !string.IsNullOrWhiteSpace(connection.FromName);
                }
            }
            catch
            {
            }

            return false;
        }

       
        public static bool TryParseLifeline(string line, ILookup<string, UMLDataType> types, int lineNumber,
            [NotNullWhen(true)] out UMLSequenceLifeline? lifeline)
        {
            var m = _lifeLineRegex.Match(line);
            if (m.Success)
            {
                string type = m.Groups["type"].Value;
                string name = m.Groups["name"].Value.Trim('\"' );
                string alias = m.Groups["alias"].Value;
                if (string.IsNullOrEmpty(alias))
                    alias = name;

                if (!types.Contains(name))
                {
                    lifeline = new UMLSequenceLifeline(type, name, alias, null, lineNumber);
                }
                else
                    lifeline = new UMLSequenceLifeline(type, name, alias, types[name].First().Id, lineNumber);
                return true;
            }
            lifeline = null;
            return false;
        }
    }
}