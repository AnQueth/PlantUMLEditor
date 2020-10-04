using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    public class SynatxFlowDocument
    {
        private Dictionary<Regex, Color> _colorCodes = new Dictionary<Regex, Color>()
        {
            {new Regex("^(\\{|\\})") , Colors.Purple},

                {new Regex("^(class|\\{\\w+\\}|interface|package|alt|opt|loop|try|group|catch|break|par|participant|actor|database|component|end)$"), Colors.Blue}
        };

        private FixedDocument _document = new FixedDocument();

        private Dictionary<Regex, (Color, bool)> _mcolorCodes = new Dictionary<Regex, (Color, bool)>()
        {
            {new Regex("(participant|actor|database|component|class|interface)\\s+\\w+\\s+$"), (Colors.Green, false ) },
              {new Regex("title.*$"), (Colors.Green, false ) },
               {new Regex("(\\:.+)$"), (Colors.DarkBlue, true) }
        };

        public FixedDocument Document
        {
            get
            {
                return _document;
            }
        }

        private static Span FindRow(DependencyObject parent)
        {
            while (true)
            {
                if (parent is Run g)
                    parent = g.Parent;
                else if (parent is Span s && s.Name == "row")
                    return s;
                else if (parent is Span s2)
                    parent = s2.Parent;
            }

            return null;
        }

        private void FormatLine(InlineCollection col, string line, ref int indentLevel)
        {
            Span spanContainer = new Span();
            spanContainer.Name = "row";

            indentLevel = UpdateSpan(line, indentLevel, spanContainer);

            col.Add(spanContainer);
        }

        private int UpdateSpan(string line, int indentLevel, Span spanContainer)
        {
            spanContainer.Inlines.Clear();

            if (string.IsNullOrEmpty(line.Trim()))
                return indentLevel;

            const char space = ' ';
            Regex r = new Regex("[ \\t\\r\\n]", RegexOptions.IgnoreCase);

            Regex tab = new Regex("^(class|\\{\\w+\\}|interface|package|alt|opt|loop|try|group|catch|break|par)$");
            Regex tabStop = new Regex("\\}|end$");
            Regex tabReset = new Regex("else\\s?.*");

            Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)| as (?<alias>\\w+))");

            if (notes.IsMatch(line))
            {
                var rr2 = new Run(line);
                rr2.Foreground = new SolidColorBrush(Colors.Red);
                rr2.FontWeight = FontWeights.Bold;

                spanContainer.Inlines.Add(new LineBreak());
                spanContainer.Inlines.Add(rr2);
                spanContainer.Inlines.Add(new LineBreak());
                spanContainer.Inlines.Add(new LineBreak());
                return indentLevel;
            }

            StringBuilder sbWordsSoFar = new StringBuilder();
            if (tabStop.IsMatch(line))
            {
                indentLevel--;
            }
            if (tabReset.IsMatch(line))
            {
                indentLevel--;
            }
            var words = r.Split(line);
            for (var x = 0; x < words.Length; x++)
            {
                var w = words[x];
                string insertText = (x != words.Length - 1 ? w + " " : w).TrimStart();
                sbWordsSoFar.Append(w);
                sbWordsSoFar.Append(" ");

                if (x == 0)
                {
                    for (int indent = 0; indent < indentLevel; indent++)
                    {
                        var rr = new Run("  ");

                        spanContainer.Inlines.Add(rr);
                    }
                }
                if (tabReset.IsMatch(w))
                {
                    indentLevel++;
                }
                if (tab.IsMatch(w))
                {
                    indentLevel++;
                }

                if (indentLevel < 0)
                    indentLevel = 0;

                bool processed = false;

                foreach (var item in _colorCodes)
                {
                    if (item.Key.IsMatch(w))
                    {
                        var rr2 = new Run(insertText);
                        rr2.Foreground = new SolidColorBrush(item.Value);

                        spanContainer.Inlines.Add(rr2);
                        processed = true;
                    }
                }

                foreach (var item in _mcolorCodes)
                {
                    if (item.Key.IsMatch(sbWordsSoFar.ToString()))
                    {
                        var rr2 = new Run(insertText);
                        rr2.Foreground = new SolidColorBrush(item.Value.Item1);
                        if (item.Value.Item2)
                            rr2.FontStyle = FontStyles.Italic;

                        spanContainer.Inlines.Add(rr2);
                        processed = true;
                    }
                }
                if (!processed)
                {
                    var rr2 = new Run(insertText);

                    rr2.Foreground = new SolidColorBrush(Colors.Black);
                    spanContainer.Inlines.Add(rr2);
                }
            }
            //var sp3 = new Span();

            //var rr3 = new Run("\n");
            //sp3.Inlines.Add(rr3);
            //spanContainer.Inlines.Add(sp3);

            spanContainer.Inlines.Add(new LineBreak());

            return indentLevel;
        }

        public void SetText(string text)
        {
            _document = new FixedDocument();

            PageContent pc = new PageContent();
            FixedPage fp = new FixedPage();

            TextBlock tb = new TextBlock();

            Regex reg = new Regex("\n");
            string[] lines = reg.Split(text);

            int indentLevel = 0;

            string oldLine = "";

            Regex newLineAfter = new Regex("participant|actor|database|component|class|interface");

            for (var x = 0; x < lines.Length; x++)
            {
                if (string.IsNullOrWhiteSpace(lines[x]))
                    continue;

                if (oldLine.StartsWith("title") || oldLine.StartsWith("@startuml"))
                    tb.Inlines.Add(new LineBreak());

                if (newLineAfter.IsMatch(oldLine) && !newLineAfter.IsMatch(lines[x]))
                {
                    tb.Inlines.Add(new LineBreak());
                }
                oldLine = lines[x].Trim();

                FormatLine(tb.Inlines, lines[x].Trim(), ref indentLevel);
            }
            fp.Children.Add(tb);

            pc.Child = fp;

            _document.Pages.Add(pc);
        }
    }
}