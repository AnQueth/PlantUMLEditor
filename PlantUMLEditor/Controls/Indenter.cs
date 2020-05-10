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
        private Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))");

        private Regex tab = new Regex("^(class|\\{\\w+\\}|interface|package|alt|opt|loop|try|group|catch|break|par)");
        private Regex tabReset = new Regex("^else\\s?.*");
        private Regex tabStop = new Regex("^\\}|end(?! note)");

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

            StringBuilder sbWordsSoFar = new StringBuilder();
            if (tabStop.IsMatch(line))
            {
                indentLevel--;
            }
            if (tabReset.IsMatch(line))
            {
                indentLevel--;
            }

            for (int indent = 0; indent < indentLevel; indent++)
            {
                sb.Append("    ");
            }

            sb.AppendLine(line);
            if (tabReset.IsMatch(line))
            {
                indentLevel++;
            }
            if (tab.IsMatch(line))
            {
                indentLevel++;
            }

            if (indentLevel < 0)
                indentLevel = 0;

            return indentLevel;
        }

        public string Process(string text)
        {
            Regex reg = new Regex("\n");
            string[] lines = reg.Split(text);

            int indentLevel = 0;

            string oldLine = "";

            StringBuilder sb = new StringBuilder();

            Regex newLineAfter = new Regex("participant|actor|database|component|class|interface");

            for (var x = 0; x < lines.Length; x++)
            {
                if (string.IsNullOrWhiteSpace(lines[x]))
                    continue;

                if (oldLine.StartsWith("title") || oldLine.StartsWith("@startuml") || (oldLine == "}" && lines[x].Trim() != "}"))
                    sb.AppendLine();

                if (newLineAfter.IsMatch(oldLine) && !newLineAfter.IsMatch(lines[x]))
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