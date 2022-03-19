using System;
using System.Text;
using System.Text.RegularExpressions;

namespace PlantUMLEditor.Controls
{
    internal class Indenter
    {
        private static readonly Regex newLineAfter = new(@"^(note.+:|end +note|\'.+|.+\'\/|left to right direction)", RegexOptions.Compiled);
        private static readonly Regex newLineBefore = new(@"^(note.+|\'.+|\/\'.+|left to right direction)", RegexOptions.Compiled);
        // private static readonly Regex notes = new("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))", RegexOptions.Compiled);

        private static readonly Regex reg = new("\n");
        private static readonly Regex removeSpaces = new(" {2,}", RegexOptions.Compiled);
        private static readonly Regex tab = new(@"^(alt|opt|loop|try|group|catch|break|par)\s+", RegexOptions.Compiled);
        private static readonly Regex tab2 = new(@"^\s*(abstract|class|interface|package|enum|together|component|database|rectangle|queue|node|folder|cloud).+\{", RegexOptions.Compiled);
        private static readonly Regex tab3 = new(@"^\s*(if\s+\(.*|repeat(?!\s*while).*|fork(?!\s*again))$", RegexOptions.Compiled);
        private static readonly Regex tabReset = new(@"^(else\s?.*|fork\s+again)", RegexOptions.Compiled);
        private static readonly Regex tabStop = new(@"^(\}|end(?! note)|endif|repeat\s+while.*)", RegexOptions.Compiled);

        private static int ProcessLine(StringBuilder? sb, string line, ref int indentLevel)
        {
            if (string.IsNullOrEmpty(line.Trim()))
            {
                return indentLevel;
            }

            //if (notes.IsMatch(line))
            //{
            //    sb.AppendLine();
            //    sb.AppendLine(line);
            //    sb.AppendLine();

            //    return indentLevel;
            //}

            line = removeSpaces.Replace(line, " ");

            ReadOnlySpan<char> r = line.AsSpan();

            StringBuilder sbNewLine = new(line.Length);
            int starts = 0;
            char oldd = '\0';

            for (int x = 0; x < r.Length; x++)
            {
                char d = r[x];

                if (d == '<' && x != 0)
                {
                    starts++;
                }
                else if (d == '>')
                {
                    starts--;
                }

                if (starts > 0 && d == ' ' && oldd != ',')
                {
                }
                else
                {
                    sbNewLine.Append(d);
                }
                if (d != ' ')
                {
                    oldd = d;
                }
            }
            line = sbNewLine.ToString();


            if (tabStop.IsMatch(line))
            {
                indentLevel--;
            }
            if (tabReset.IsMatch(line))
            {
                indentLevel--;
            }
            if (sb != null)
            {
                for (int indent = 0; indent < indentLevel; indent++)
                {
                    sb.Append("    ");
                }

                sb.AppendLine(line);
            }
            if (tabReset.IsMatch(line))
            {
                indentLevel++;
            }
            if (tab.IsMatch(line) || tab2.IsMatch(line) || tab3.IsMatch(line))
            {
                indentLevel++;
            }

            if (indentLevel < 0)
            {
                indentLevel = 0;
            }

            return indentLevel;
        }

        public static int GetIndentLevelForLine(string text, int line)
        {
            string[] lines = reg.Split(text);

            int indentLevel = 0;

            for (int x = 0; x < lines.Length && x <= line; x++)
            {
                _ = ProcessLine(null, lines[x].Trim(), ref indentLevel);
            }

            return indentLevel;
        }

        public static string Process(string text, bool removeLines)
        {
            string[] lines = reg.Split(text);

            int indentLevel = 0;

            string oldLine = "";

            StringBuilder sb = new();

            for (int x = 0; x < lines.Length; x++)
            {
                if (string.IsNullOrWhiteSpace(lines[x]) && removeLines)
                {
                    continue;
                }
                else if (string.IsNullOrWhiteSpace(lines[x]))
                {
                    sb.AppendLine();
                }

                if (oldLine.StartsWith("title", StringComparison.InvariantCultureIgnoreCase) || oldLine.StartsWith("@startuml",
                    StringComparison.InvariantCultureIgnoreCase) || (oldLine == "}" && lines[x].Trim() != "}"))
                {
                    if (removeLines)
                    {
                        sb.AppendLine();
                    }
                }
                if (!string.IsNullOrWhiteSpace(oldLine) &&
                    (newLineAfter.IsMatch(oldLine) || newLineBefore.IsMatch(lines[x].Trim())))
                {
                    if (removeLines)
                    {
                        sb.AppendLine();
                    }
                }
                oldLine = lines[x].Trim();

                ProcessLine(sb, lines[x].Trim(), ref indentLevel);
            }
            return sb.ToString();
        }
    }
}