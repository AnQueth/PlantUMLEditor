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
        private static readonly Regex _lifeLineRegex = new(@"^(?<type>participant|actor|control|component|database|boundary|entity|collections)\s+\""*(?<name>[\w\\ \.]+?(\s*\<((?<generics>[\s\w]+)\,*)*\>)*)\""*(\s+as (?<alias>[\w]+|\"".+\""))*? *([\#\<]+.*)*$", RegexOptions.Compiled);
        private static readonly Regex _blockSection = new("(?<type>alt|loop|else|par|opt|try|group|catch|finally|break)(?<text>.*)");

        private static readonly Regex lineConnectionRegex = new(@"^([a-zA-Z0-9\>\<\,\[\]]+|[\-<>\]\[\#]+)\s*([a-zA-Z0-9\-\.\>\<\\\/\]\[\#]+)\s*([a-zA-Z0-9\-><]*)\s*\:*\s*(.+)*$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

        private static readonly Regex other = new("^(activate|deactivate)\\w*", RegexOptions.Compiled);

        private static UMLSignature? CheckActionOnType(UMLDataType toType, string actionSignature)
        {
            UMLSignature? action = toType.Methods.Find(p => p.Signature == actionSignature);
            if (action == null)
            {
                action = toType.Properties.Find(p => p.Signature == actionSignature);
            }

            if (action != null)
            {
                return action;
            }

            foreach (UMLDataType? item in toType.Bases)
            {
                action = CheckActionOnType(item, actionSignature);
                if (action != null)
                {
                    return action;
                }
            }
            return null;
        }

        private static string Clean(string name)
        {
            string? t = name.Trim();
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
                foreach (UMLDataType? toType in types[to.DataTypeId])
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
                    {
                        action = new UMLReturnFromMethod(previous.Action);
                    }
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
                {
                    action = new UMLUnknownAction(actionSignature);
                }
            }

            return action;
        }

        private static async Task<UMLSequenceDiagram?> ReadDiagram(StreamReader sr, LockedList<UMLClassDiagram> classDiagrams, string fileName, bool justLifeLines)
        {
            ILookup<string, UMLDataType>? types = classDiagrams.SelectMany(p => p.DataTypes).Where(p => p is UMLClass or UMLInterface or UMLStruct).ToLookup(p => p.Name);

            UMLSequenceDiagram d = new(string.Empty, fileName);
            bool started = false;
            string? line = null;

            Stack<UMLSequenceBlockSection> activeBlocks = new();

            int lineNumber = 0;
            UMLSequenceConnection? previous = null;

            CommonParsings cp = new CommonParsings();

            while ((line = await sr.ReadLineAsync()) != null)
            {
                lineNumber++;
                line = line.Trim();

                if (line.StartsWith("class", StringComparison.Ordinal) || line.StartsWith("interface", StringComparison.Ordinal) || line.StartsWith("package", StringComparison.Ordinal))
                {
                    return null;
                }

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






                if (line == "'@@novalidate")
                {
                    d.ValidateAgainstClasses = false;
                    continue;
                }


                if (line.StartsWith("...", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("||", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("autoactivate", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("return", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("box", StringComparison.Ordinal))
                {
                    continue;
                }
                if (line.StartsWith("end box", StringComparison.Ordinal))
                {
                    continue;
                }
                if (line.StartsWith("==", StringComparison.Ordinal) && line.EndsWith("==", StringComparison.Ordinal))
                {
                    continue;
                }

                if (cp.CommonParsing(line, (str) =>
                {

                },
               (str, alias) =>
               {

               },
               (str) =>
               {

               },
               (str) =>
               {

               },
               (str) =>
               {

               }
               ))
                {
                    continue;
                }

                if (line.StartsWith("autonumber", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("header", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("footer", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("newpage", StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryParseLifeline(line, types, lineNumber, out UMLSequenceLifeline? lifeline))
                {
                    d.LifeLines.Add(lifeline);


                    continue;
                }

                if (!justLifeLines)
                {



                    if (TryParseUMLSequenceBlockSection(line, lineNumber, out UMLSequenceBlockSection? sectionBlock))
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
                        //if (activeBlocks.Count > 0)
                        //{
                        //    var p = activeBlocks.Peek().SectionType;
                        //    if (p is UMLSequenceBlockSection.SectionTypes.If
                        //        or UMLSequenceBlockSection.SectionTypes.Parrallel
                        //        or UMLSequenceBlockSection.SectionTypes.Try
                        //        )
                        //    {
                        //        _ = activeBlocks.Pop();
                        //    }
                        //}
                    }
                    else if (other.IsMatch(line))
                    {
                        UMLSequenceOther uMLSequenceOther = new(lineNumber, line);
                        if (activeBlocks.Count == 0)
                        {
                            d.Entities.Add(uMLSequenceOther);
                        }
                        else
                        {
                            activeBlocks.Peek().Entities.Add(uMLSequenceOther);
                        }
                    }
                    else if (TryParseAllConnections(line, d, types, previous, lineNumber, out UMLSequenceConnection? connection))
                    {


                        if (activeBlocks.Count == 0)
                        {
                            d.Entities.Add(connection);
                        }
                        else
                        {
                            activeBlocks.Peek().Entities.Add(connection);
                        }

                        previous = connection;
                    }
                    else
                    {
                        d.AddLineError(line, lineNumber);
                    }
                }


            }

            return d;
        }

        private static bool TryParseUMLSequenceBlockSection(string line, int lineNumber,
            [NotNullWhen(true)] out UMLSequenceBlockSection? block)
        {
            Match? blockSection = _blockSection.Match(line);
            if (blockSection.Success)
            {
                string? name = blockSection.Groups["type"].Value;
                string text = blockSection.Groups["text"].Value;

                switch (name.ToLowerInvariant())
                {
                    case "opt":
                    case "alt":
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.If, lineNumber);
                        return true;

                    case "else":
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Else, lineNumber);
                        return true;

                    case "par":
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Parrallel, lineNumber);
                        return true;

                    case "try":
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Try, lineNumber);
                        return true;

                    case "catch":
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Catch, lineNumber);
                        return true;

                    case "finally":
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Finally, lineNumber);
                        return true;

                    case "break":
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Break, lineNumber);
                        return true;

                    case "loop":
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Loop, lineNumber);
                        return true;

                    case "group":
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Group, lineNumber);
                        return true;

                    case "critical":
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Critical, lineNumber);
                        return true;
                }
            }
            block = null;
            return false;
        }
        private static bool TryParseConnection(string fromAlias, string arrow, string toAlias,
            string actionSignature, UMLSequenceDiagram d, ILookup<string, UMLDataType> types, UMLSequenceConnection? previous,
                int lineNumber,
            [NotNullWhen(true)] out UMLSequenceConnection? connection)
        {
            connection = null;



            UMLSequenceLifeline? from = d.LifeLines.Find(p => p.Alias == fromAlias);

            UMLSignature? action = null;
            UMLSequenceLifeline? to = d.LifeLines.Find(p => p.Alias == toAlias);
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

                UMLSequenceLifeline? ft = d.LifeLines.Find(p => p.Alias == fromAlias);
                if (ft == null)
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

                UMLSequenceLifeline? to = d.LifeLines.Find(p => p.Alias == toAlias);

                UMLSignature? action = null;
                if (to != null && to.DataTypeId != null && types.Contains(to.DataTypeId))
                {
                    action = GetActionSignature(actionSignature, types, to, previous, d);
                }

                connection = new UMLSequenceConnection(to, true, action, toAlias, lineNumber);

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


                Match? m = lineConnectionRegex.Match(line);
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
            catch (Exception)
            {
                diagram.AddLineError(line, lineNumber);
            }

            return false;
        }


        public static bool TryParseLifeline(string line, ILookup<string, UMLDataType> types, int lineNumber,
            [NotNullWhen(true)] out UMLSequenceLifeline? lifeline)
        {
            Match? m = _lifeLineRegex.Match(line);
            if (m.Success)
            {
                string type = m.Groups["type"].Value;
                string name = m.Groups["name"].Value.Trim('\"');
                string alias = m.Groups["alias"].Value;
                if (string.IsNullOrEmpty(alias))
                {
                    alias = name;
                }

                if(alias.Contains('\"'))
                {
                    var n = alias;
                    alias = name;
                    name = n;
                }

                if (!types.Contains(name))
                {
                    lifeline = new UMLSequenceLifeline(type, name, alias, null, lineNumber);
                }
                else
                {
                    lifeline = new UMLSequenceLifeline(type, name, alias, types[name].First().Id, lineNumber);
                }

                return true;
            }
            lifeline = null;
            return false;
        }
    }
}