using System;
using System.Text;

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


        private bool _swallowingNotes;
        private bool _swallowingSkinParams;
        private bool _swallowingComment;
        private readonly ParseFlags _parseFlag;
        private readonly StringBuilder _sbReader = new();

        public CommonParsings(ParseFlags parseFlag)
        {
            _parseFlag = parseFlag;
        }

        internal bool CommonParsing(string line, Action<string> otherCB, Action<string> noteCB,
            Action<string> skinParamsCB, Action<string> commentCB, Action<string> precompilerCB)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return true;
            }

            if ((_parseFlag & ParseFlags.Direction) == ParseFlags.Direction)
            {

                if (line == "left to right direction")
                {
                    otherCB(line);
                    return true;
                }
            }
            if ((_parseFlag & ParseFlags.Comment) == ParseFlags.Comment)
            {
                if (line.StartsWith("'", StringComparison.Ordinal) && !line.StartsWith("'/", StringComparison.Ordinal))
                {
                    commentCB(line);
                    return true;
                }
                else if (line.StartsWith("/'", StringComparison.Ordinal) && line.EndsWith("'/", StringComparison.Ordinal))
                {
                    commentCB(line);
                    return true;
                }
                else if (line.StartsWith("/'", StringComparison.Ordinal))
                {
                    _sbReader.Clear();
                    _sbReader.AppendLine(line);
                    _swallowingComment = true;
                    return true;
                }

                else if (_swallowingComment && line.StartsWith("'/", StringComparison.Ordinal))
                {
                    _sbReader.AppendLine(line);
                    commentCB(_sbReader.ToString());
                    _swallowingComment = false;
                    return true;
                }
                else if (_swallowingComment)
                {
                    _sbReader.AppendLine(line);
                    return true;
                }
            }
            if ((_parseFlag & ParseFlags.PreProcessor) == ParseFlags.PreProcessor)
            {
                if (line.StartsWith("!", StringComparison.Ordinal))
                {
                    precompilerCB(line);
                    return true;
                }
            }
            if ((_parseFlag & ParseFlags.SkinParam) == ParseFlags.SkinParam)
            {
                if (line.StartsWith("skinparam", StringComparison.Ordinal))
                {
                    if (line.EndsWith("{", StringComparison.Ordinal))
                    {
                        _sbReader.Clear();
                        _sbReader.AppendLine(line);
                        _swallowingSkinParams = true;
                    }
                    else
                    {
                        skinParamsCB(line);
                        _swallowingSkinParams = false;
                    }
                    return true;

                }

                if (_swallowingSkinParams && line == "}")
                {

                    _sbReader.AppendLine(line);
                    skinParamsCB(_sbReader.ToString());
                    _swallowingSkinParams = false;
                    return true;
                }

                if (_swallowingSkinParams)
                {
                    _sbReader.AppendLine(line);
                    return true;
                }
            }
            if ((_parseFlag & ParseFlags.Note) == ParseFlags.Note)
            {
                if ((line.StartsWith("/", StringComparison.Ordinal) && line.Contains("note", StringComparison.Ordinal) && !line.Contains("end", StringComparison.Ordinal)) ||
                    line.StartsWith("note", StringComparison.Ordinal) ||
                    line.StartsWith("hnote", StringComparison.Ordinal) ||
                    line.StartsWith("rnote", StringComparison.Ordinal))
                {

                    if (line.Contains(":", StringComparison.Ordinal) && !line.Contains("::", StringComparison.Ordinal))
                    {
                        noteCB(line);
                        _swallowingNotes = false;
                        return true;
                    }
                    else
                    {
                        _sbReader.Clear();
                        _sbReader.AppendLine(line);
                        _swallowingNotes = true;
                        return true;
                    }




                }

                if (line.StartsWith("end note", StringComparison.Ordinal) ||
                    line.StartsWith("endhnote", StringComparison.Ordinal) ||
                    line.StartsWith("endrnote", StringComparison.Ordinal))
                {

                    _sbReader.AppendLine(line);
                    noteCB(_sbReader.ToString());
                    _swallowingNotes = false;
                    return true;
                }

                if (_swallowingNotes)
                {

                    _sbReader.AppendLine(line);

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