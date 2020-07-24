using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices.ActiveDirectory;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    internal class Indenter
    {
        private static Regex newLineAfter = new Regex("note.+|end +note|\\}", RegexOptions.Compiled);
        private static Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))", RegexOptions.Compiled);

        private static Regex reg = new Regex("\n");
        private static Regex removeSpaces = new Regex(" {2,}", RegexOptions.Compiled);
        private static Regex tab = new Regex("^(\\{\\w+\\}|alt|opt|loop|try|group|catch|break|par)\\s+", RegexOptions.Compiled);
        private static Regex tab2 = new Regex("^\\s*(class|interface|package|enum|together)[ \\.\\w+]+\\{", RegexOptions.Compiled);
        private static Regex tabReset = new Regex("^else\\s?.*", RegexOptions.Compiled);
        private static Regex tabStop = new Regex("^(\\}|end(?! note))", RegexOptions.Compiled);

        private int ProcessLine(StringBuilder sb, string line, ref int indentLevel)
        {
            if (string.IsNullOrEmpty(line.Trim()))
                return indentLevel;

            //if (notes.IsMatch(line))
            //{
            //    sb.AppendLine();
            //    sb.AppendLine(line);
            //    sb.AppendLine();

            //    return indentLevel;
            //}

            line = removeSpaces.Replace(line, " ");

            ReadOnlySpan<char> r = line.AsSpan();

            StringBuilder sbNewLine = new StringBuilder(line.Length);
            int starts = 0;
            foreach (var d in r)
            {
                if (d == '<')
                {
                    starts++;
                }
                else if (d == '>')
                    starts--;

                if (starts > 0 && d == ' ')
                {
                }
                else
                {
                    sbNewLine.Append(d);
                }
            }
            line = sbNewLine.ToString();

            StringBuilder sbWordsSoFar = new StringBuilder();
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
            if (tab.IsMatch(line) || tab2.IsMatch(line))
            {
                indentLevel++;
            }

            if (indentLevel < 0)
                indentLevel = 0;

            return indentLevel;
        }

        public int GetIndentLevelForLine(string text, int line)
        {
            string[] lines = reg.Split(text);

            int indentLevel = 0;

            for (var x = 0; x < lines.Length && x <= line; x++)
            {
                ProcessLine(null, lines[x].Trim(), ref indentLevel);
            }

            return indentLevel;
        }

        public string Process(string text)
        {
            string[] lines = reg.Split(text);

            int indentLevel = 0;

            string oldLine = "";

            StringBuilder sb = new StringBuilder();

            for (var x = 0; x < lines.Length; x++)
            {
                if (string.IsNullOrWhiteSpace(lines[x]))
                    continue;

                if (oldLine.StartsWith("title", StringComparison.InvariantCultureIgnoreCase) || oldLine.StartsWith("@startuml",
                    StringComparison.InvariantCultureIgnoreCase) || (oldLine == "}" && lines[x].Trim() != "}"))
                    sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(oldLine) && newLineAfter.IsMatch(oldLine))
                {
                    sb.AppendLine();
                }
                oldLine = lines[x].Trim();

                ProcessLine(sb, lines[x].Trim(), ref indentLevel);
            }
            return sb.ToString();
        }
    }
}