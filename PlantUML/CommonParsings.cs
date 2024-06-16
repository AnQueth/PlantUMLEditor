using System;
using System.Text;

namespace PlantUML
{
    public class CommonParsings
    {



        private bool _swallowingNotes;
        private bool _swallowingSkinParams;
        private bool _swallowingLegend;
        private bool _swallowingComment;
        private string? _currentNotesAlias;

        private readonly StringBuilder _sbReader = new();

        public CommonParsings()
        {

        }

        internal bool CommonParsing(string line, Action<string> otherCB, Action<string, string?> noteCB,
            Action<string> skinParamsCB, Action<string> commentCB, Action<string> precompilerCB)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return true;
            }

            if (line == "@enduml")
            {
                return true;
            }

            if (line.Contains("[hidden]"))
            {
                otherCB(line);
                return true;
            }

            if (line == "left to right direction")
            {
                otherCB(line);
                return true;
            }
            if (line.StartsWith("show", StringComparison.Ordinal))
            {
                otherCB(line);
                return true;
            }
            if (line.StartsWith("remove", StringComparison.Ordinal))
            {
                otherCB(line);
                return true;
            }
            if (line.StartsWith("hide", StringComparison.Ordinal))
            {
                otherCB(line);
                return true;
            }

            if (line.StartsWith("scale", StringComparison.Ordinal))
            {
                otherCB(line);
                return true;
            }

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

            if (line.StartsWith("!", StringComparison.Ordinal))
            {
                precompilerCB(line);
                return true;
            }

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

            if (line.StartsWith("legend", StringComparison.Ordinal))
            {
                
           
                    _sbReader.AppendLine(line);
                    _swallowingLegend = true;
          
                return true;

            }

            if (_swallowingLegend && line == "endlegend")
            {

                _sbReader.AppendLine(line);
       
                _swallowingLegend = false;
                return true;
            }

            if (_swallowingLegend)
            {
                _sbReader.AppendLine(line);
                return true;
            }

            if ((line.StartsWith("/", StringComparison.Ordinal) && line.Contains("note", StringComparison.Ordinal) && !line.Contains("end", StringComparison.Ordinal)) ||
                line.StartsWith("note", StringComparison.Ordinal) ||
                line.StartsWith("hnote", StringComparison.Ordinal) ||
                line.StartsWith("rnote", StringComparison.Ordinal))
            {
                string[] items = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (items.Length > 2)
                {
                    if (items[^2] == "as")
                    {
                        _currentNotesAlias = items[^1];
                    }

                }
                else
                {
                    _currentNotesAlias = null;
                }
                if (line.Contains("\"", StringComparison.Ordinal) && line.Contains(" as ", StringComparison.Ordinal) && !line.Contains(":", StringComparison.Ordinal))
                {


                    noteCB(line, _currentNotesAlias);
                    _swallowingNotes = false;
                    return true;
                }
                if (line.Contains(":", StringComparison.Ordinal) && !line.Contains("::", StringComparison.Ordinal))
                {
                    noteCB(line, null);
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
                noteCB(_sbReader.ToString(), _currentNotesAlias);
                _swallowingNotes = false;
                return true;
            }

            if (_swallowingNotes)
            {

                _sbReader.AppendLine(line);

                return true;
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