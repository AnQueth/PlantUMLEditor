using System;
using System.Text.RegularExpressions;

namespace PlantUML
{
    public class CommonParsings
    {
        [Flags]
        public enum ParseFlags
        {
            Tite = 1,
            Note = 2,
            Direction = 4,
            SkinParam = 8,
            PreProcessor = 16,
            Comment = 32,
            All = Tite | Note | Direction | SkinParam | PreProcessor | Comment

        }
        private static readonly Regex notes = new("note *((?<sl>(?<placement>\\w+) of (?<target>[\\\"\\w\\,\\s\\<\\>\\[\\]]+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\\\"(?<text>[\\w\\W]+)\\\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>[\\\"\\w\\,\\s\\<\\>]+)| as (?<alias>\\w+))", RegexOptions.Compiled);
        private static readonly Regex skinparams = new Regex(@"skinparam[\w\s]+(?<sl>\{*)", RegexOptions.Compiled);
        private bool _swallowingNotes;
        private bool _swallowingSkinParams;
        private readonly ParseFlags _parseFlag;

        public CommonParsings(ParseFlags parseFlag)
        {
            _parseFlag = parseFlag;
        }

        internal bool CommonParsing(string line, Action<string>? noteCreatedCB = null, Action<string>? notesContentCB = null, Action<string>? endNoteCB = null)
        {
            if ((_parseFlag & ParseFlags.Direction) == ParseFlags.Direction)
            {

                if (line == "left to right direction")
                {
                    return true;
                }
            }
            if ((_parseFlag & ParseFlags.Comment) == ParseFlags.Comment)
            {
                if (line.StartsWith("'", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            if ((_parseFlag & ParseFlags.PreProcessor) == ParseFlags.PreProcessor)
            {
                if (line.StartsWith("!", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            if ((_parseFlag & ParseFlags.SkinParam) == ParseFlags.SkinParam)
            {
                if (skinparams.IsMatch(line))
                {
                    if (skinparams.Match(line).Groups["sl"].Length > 0)
                    {
                        _swallowingSkinParams = true;
                    }
                    else
                    {
                        _swallowingSkinParams = false;
                    }
                    return true;

                }

                if (_swallowingSkinParams && line == "}")
                {
                    _swallowingSkinParams = false;
                }

                if (_swallowingSkinParams)
                {
                    return true;
                }
            }
            if ((_parseFlag & ParseFlags.Note) == ParseFlags.Note)
            {
                if (notes.IsMatch(line))
                {
                    Match? m = notes.Match(line);
                    if (!m.Groups["sl"].Success)
                    {
                        _swallowingNotes = true;
                    }
                    else
                    {
                        _swallowingNotes = false;
                    }

                    if (noteCreatedCB is not null)
                    {
                        noteCreatedCB(line);
                    }

                    if (!_swallowingNotes)
                    {
                        return true;
                    }
                }

                if (line.StartsWith("end note", StringComparison.Ordinal))
                {
                    if (endNoteCB is not null)
                    {
                        endNoteCB(line);
                    }

                    _swallowingNotes = false;
                    return true;
                }

                if (_swallowingNotes)
                {
                    if (notesContentCB is not null)
                    {
                        notesContentCB(line);
                    }

                    return true;
                }
            }
            return false;
        }

        internal bool ParseStart(string line, Action<string> cb)
        {

            if (line.StartsWith("@startuml", StringComparison.Ordinal))
            {
                if (line.Length > 9)
                {
                    cb(line[9..].Trim());
                }

                return true;
            }
            return false;
        }

        internal bool ParseTitle(string line, Action<string> cb)
        {
            if (line.StartsWith("title", StringComparison.Ordinal))
            {
                cb(line[5..].Trim());
                return true;
            }
            return false;
        }
    }
}