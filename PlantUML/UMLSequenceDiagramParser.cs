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
using static System.Collections.Specialized.BitVector32;

namespace PlantUML
{
    public static class UMLSequenceDiagramParser
    {
        private static readonly Regex _blockSection = new("^(?<type>alt|loop|end|ref|else|critical|par|opt|try|group|catch|finally|break)(?<text>.*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _lifeLineRegex = new(@"^(?<type>participant|create|actor|control|component|database|boundary|entity|collections)\s+\""*(?<name>[\w\\ \.]+?(\s*\<((?<generics>[\s\w]+)\,*)*\>)*)\""*(\s+as (?<alias>[\w]+|\"".+\""))*? *([\#\<]+.*)*$", RegexOptions.Compiled |  RegexOptions.CultureInvariant);
        private static readonly Regex lineConnectionRegex = new(@"^([a-zA-Z0-9\><\,\[\]]+|[\-<>\]\[\#]+)\s*([a-zA-Z0-9\-\.\>\\\/<\]\[\#]+)\s*([a-zA-Z0-9\-><]*)\s*\:*\s*(.+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

        private static readonly Regex other = new("^(activate|deactivate|destroy)\\w*", RegexOptions.Compiled);

        public static async Task<UMLSequenceDiagram?> ReadFile(string file, LockedList<UMLClassDiagram> types, bool justLifeLines)
        {
            using StreamReader sr = new(file);
            UMLSequenceDiagram? c = await ReadDiagram(sr, types, file, justLifeLines);

            return c;
        }

        public static async Task<UMLSequenceDiagram?> ReadString(string s, LockedList<UMLClassDiagram> types, bool justLifeLines)
        {
            using TextReader tr = new StringReader(s);
            UMLSequenceDiagram? c = await ReadDiagram(tr, types, "", justLifeLines);

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

                // quick checks on group spans
                var g1 = m.Groups[1].ValueSpan;
                var g2 = m.Groups[2].ValueSpan;
                var g3 = m.Groups[3].ValueSpan;

                if (!(g1.Contains('-') || g1.Contains('.')) && !(g2.Contains('.') || g2.Contains('-')))
                {
                    return false;
                }

                // determine aliases/arrow using spans, convert to string only when needed
                string? fromAlias = (g1.Length > 0 && g1[0] is not '<' and not '-' and not '>') ? g1.ToString() : null;
                ReadOnlySpan<char> arrowSpan = fromAlias == null ? g1 : g2;
                ReadOnlySpan<char> toAliasSpan = fromAlias == null ? (g2.Length == 1 && g2[0] == '-' ? g3 : g2) : g3;

                string arrow = arrowSpan.ToString();
                string toAlias = toAliasSpan.ToString();

                int colonIndex = line.IndexOf(':');
                string actionSignature;
                if (colonIndex != -1)
                {
                    ReadOnlySpan<char> span = line.AsSpan(colonIndex + 1).Trim();
                    actionSignature = span.IsEmpty ? string.Empty : span.ToString();
                }
                else
                {
                    actionSignature = string.Empty;
                }

                if (fromAlias == null)
                {
                    if (arrow.StartsWith("<", StringComparison.Ordinal))
                    {
                        if (TryParseReturnToEmpty(toAlias, arrow, actionSignature, diagram, types, previous, lineNumber, line, out connection))
                        {
                            return true;
                        }
                    }
                    else if (TypeParseConnectionFromEmpty(arrow, toAlias, actionSignature, diagram, types, previous, lineNumber, line, out connection))
                    {
                        return true;
                    }
                }
                else if (TryParseConnection(fromAlias, arrow, toAlias, actionSignature, diagram, types, previous, lineNumber, line, out connection))
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

                ReadOnlySpan<char> nameSpan = m.Groups["name"].ValueSpan;
                ReadOnlySpan<char> aliasSpan = m.Groups["alias"].ValueSpan;

                string name = nameSpan.IsEmpty ? string.Empty : nameSpan.ToString().Trim('"');
                string alias = aliasSpan.IsEmpty ? string.Empty : aliasSpan.ToString();

                if (string.IsNullOrEmpty(alias))
                {
                    alias = name;
                }

                if (alias.Contains('"'))
                {
                    var n = alias;
                    alias = name;
                    name = n;
                }

                if (!types.Contains(name))
                {
                    lifeline = new UMLSequenceLifeline(type, name, alias, null, lineNumber, line);
                }
                else
                {
                    lifeline = new UMLSequenceLifeline(type, name, alias, types[name].First().Id, lineNumber, line);
                }

                return true;
            }
            lifeline = null;
            return false;
        }

        private static UMLSignature? CheckActionOnType(UMLDataType toType, string actionSignature, bool laxMode)
        {
            UMLSignature? action = null;
            if (laxMode)
            {
                action = toType.Methods.Find(p => p.Signature.Contains(actionSignature, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                action = toType.Methods.Find(p => p.Signature == actionSignature);
            }

            if (action == null)
            {
                if (laxMode)
                {
                    action = toType.Properties.Find(p => p.Signature.Contains(actionSignature, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    action = toType.Properties.Find(p => p.Signature == actionSignature);
                }
            }

            if (action != null)
            {
                return action;
            }

            if (CheckBaseAndInterfaces(toType, actionSignature, laxMode, out UMLSignature actionFound))
            {
                return actionFound;
            }

            return null;
        }

        private static bool CheckBaseAndInterfaces(UMLDataType toType, string actionSignature, bool laxMode,
            out UMLSignature actionFound)
        {
            // avoid LINQ Union allocation; iterate bases and interfaces separately
            foreach (UMLDataType? item in toType.Bases)
            {
                var action = CheckActionOnType(item, actionSignature, laxMode);
                if (action != null)
                {
                    actionFound = action;
                    return true;
                }

                if (CheckBaseAndInterfaces(item, actionSignature, laxMode, out UMLSignature actionFromBase))
                {
                    actionFound = actionFromBase;
                    return true;
                }
            }

            foreach (UMLDataType? item in toType.Interfaces)
            {
                var action = CheckActionOnType(item, actionSignature, laxMode);
                if (action != null)
                {
                    actionFound = action;
                    return true;
                }

                if (CheckBaseAndInterfaces(item, actionSignature, laxMode, out UMLSignature actionFromBase))
                {
                    actionFound = actionFromBase;
                    return true;
                }
            }

            actionFound = null!;
            return false;
        }

        private static UMLSignature GetActionSignature(string actionSignature, ILookup<string, UMLDataType> types,
            UMLSequenceLifeline? to, UMLSequenceConnection? previous, UMLSequenceDiagram d)
        {
            UMLSignature? action = null;

            ReadOnlySpan<char> span = actionSignature.AsSpan().Trim();

            // quoted custom action
            if (span.Length >= 2 && span[0] == '"' && span[span.Length - 1] == '"')
            {
                action = new UMLCustomAction(actionSignature);
                return action;
            }

            if (to != null && to.DataTypeId != null)
            {
                // cache the lookup result to avoid multiple indexer work
                var candidates = types[to.DataTypeId];
                foreach (UMLDataType? toType in candidates)
                {
                    action = CheckActionOnType(toType, actionSignature, d.LaxMode);

                    if (action != null)
                    {
                        break;
                    }
                }
            }

            if (action == null)
            {
                // use span-based prefix checks to avoid allocating substrings
                if (span.StartsWith("<<create>>".AsSpan()))
                {
                    action = new UMLCreateAction(actionSignature);
                }
                else if (span.StartsWith("return".AsSpan()))
                {
                    if (previous != null && previous.Action != null)
                    {
                        action = new UMLReturnFromMethod(previous.Action);
                    }
                    else
                    {
                        ReadOnlySpan<char> restSpan = span.Slice(6).Trim();
                        string rest = restSpan.IsEmpty ? string.Empty : restSpan.ToString();

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

        private static void IfRefBlockPop(Stack<UMLSequenceBlockSection> activeBlocks)
        {
            if (activeBlocks.Count > 0)
            {
                var top = activeBlocks.Peek();
                if (top.SectionType == UMLSequenceBlockSection.SectionTypes.Ref)
                {
                    activeBlocks.Pop();
                }
            }
        }

        private static async Task<UMLSequenceDiagram?> ReadDiagram(TextReader sr, LockedList<UMLClassDiagram> classDiagrams, string fileName, bool justLifeLines)
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
                    d.Title = line.AsSpan(9).Trim().ToString();
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

                if (line == "'@@laxmode")
                {
                    d.LaxMode = true;
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
                    if (TryParseUMLSequenceBlockSection(line, lineNumber, activeBlocks, out UMLSequenceBlockSection? sectionBlock))
                    {
                        if(sectionBlock == null)
                        {
                            continue;
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

                        if (connection.Action is UMLReturnFromMethod || connection.Action is UMLLifelineReturnAction)
                        {
                            previous = null;
                        }
                        else
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

        private static bool TryParseConnection(string fromAlias, string arrow, string toAlias,
            string actionSignature, UMLSequenceDiagram d, ILookup<string, UMLDataType> types,
              UMLSequenceConnection? previous,
                int lineNumber, string line,
            [NotNullWhen(true)] out UMLSequenceConnection? connection)
        {
            connection = null;

            UMLSequenceLifeline? from = d.LifeLines.Find(p => p.Alias == fromAlias);

            UMLSignature? action = null;
            UMLSequenceLifeline? to = d.LifeLines.Find(p => p.Alias == toAlias);
            if (to != null)
            {
                action = GetActionSignature(actionSignature, types, to, previous, d);
            }
            connection = new UMLSequenceConnection(from, to, action, fromAlias,
                toAlias, true, true, lineNumber, line);

            return true;
        }

        private static bool TryParseReturnToEmpty(string fromAlias, string arrow,
            string actionSignature, UMLSequenceDiagram d, ILookup<string, UMLDataType> types,
              UMLSequenceConnection? previous,
            int lineNumber, string line,
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
                connection = new UMLSequenceConnection(ft, method, lineNumber, line);

                return true;
            }

            connection = null;

            return false;
        }

        private static bool TryParseUMLSequenceBlockSection(string line, int lineNumber,
                Stack<UMLSequenceBlockSection> activeBlocks,  out UMLSequenceBlockSection? block)
        {
            Match? blockSection = _blockSection.Match(line);
            if (blockSection.Success)
            {
                string? name = blockSection.Groups["type"].Value;
                string text = blockSection.Groups["text"].Value;

                switch (name.ToLowerInvariant())
                {
                    case "end":
                        if (activeBlocks.Count > 0)
                        {
                            activeBlocks.Pop();
                        }
                        block = null;
                        return true;

                    case "opt":
                    case "alt":
                        IfRefBlockPop(activeBlocks);
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.If, lineNumber);
                        return true;

                    case "else":
                        IfRefBlockPop(activeBlocks);
                        if (activeBlocks.Count > 0)
                            activeBlocks.Pop();
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Else, lineNumber);
                        return true;

                    case "par":
                        IfRefBlockPop(activeBlocks);
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Parrallel, lineNumber);
                        return true;

                    case "try":
                        IfRefBlockPop(activeBlocks);
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Try, lineNumber);
                        return true;

                    case "catch":
                        IfRefBlockPop(activeBlocks);
                        if (activeBlocks.Count > 0)
                            activeBlocks.Pop();
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Catch, lineNumber);
                        return true;

                    case "ref":
                        IfRefBlockPop(activeBlocks);
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Ref, lineNumber);
                        return true;

                    case "finally":
                        IfRefBlockPop(activeBlocks);
                        if (activeBlocks.Count > 0)
                            activeBlocks.Pop();
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Finally, lineNumber);
                        return true;

                    case "break":
                        IfRefBlockPop(activeBlocks);
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Break, lineNumber);
                        return true;

                    case "loop":
                        IfRefBlockPop(activeBlocks);
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Loop, lineNumber);
                        return true;

                    case "group":
                        IfRefBlockPop(activeBlocks);
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Group, lineNumber);
                        return true;

                    case "critical":
                        IfRefBlockPop(activeBlocks);
                        block = new UMLSequenceBlockSection(text, UMLSequenceBlockSection.SectionTypes.Critical, lineNumber);
                        return true;
                }
            }
            block = null;
            return false;
        }

        private static bool TypeParseConnectionFromEmpty(string arrow, string toAlias,
            string actionSignature, UMLSequenceDiagram d,
            ILookup<string, UMLDataType> types, UMLSequenceConnection? previous, int lineNumber,
            string line,
            [NotNullWhen(true)] out UMLSequenceConnection? connection)
        {
            if (arrow.StartsWith("-", StringComparison.Ordinal))
            {
                UMLSequenceLifeline? to = d.LifeLines.Find(p => p.Alias == toAlias);

                UMLSignature? action = null;
                if (to != null && to.DataTypeId != null && types.Contains(to.DataTypeId))
                {
                    action = GetActionSignature(actionSignature, types, to, previous, d);
                }

                connection = new UMLSequenceConnection(to, true, action, toAlias, lineNumber, line);

                return true;
            }

            connection = null;
            return false;
        }
    }
}