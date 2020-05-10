using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    public class ColorCoding
    {
        private Dictionary<Regex, Color> _colorCodes = new Dictionary<Regex, Color>()
        {
             {new Regex("(@startuml|@enduml)") , Colors.Gray},
            {new Regex("^\\s*(class|interface)\\s+.+?{([\\s.\\w\\W]+?)}", RegexOptions.IgnoreCase | RegexOptions.Multiline), Colors.Firebrick },
                {new Regex("^\\s*(title|class|\\{\\w+\\}|interface|package|alt|opt|loop|try|group|catch|break|par|participant|actor|database|component|end)\\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase), Colors.Blue}
        };

        private Dictionary<Regex, (Color, bool)> _mcolorCodes = new Dictionary<Regex, (Color, bool)>()
        {
            {new Regex("^\\s*(participant|actor|database|component|class|interface)\\s+\\w+\\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase), (Colors.Green, false ) },

               {new Regex("(\\:.+)"), (Colors.DarkBlue, true) }
        };

        private Regex brackets = new Regex("(\\{|\\})");
        private Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)[.\\s\\S\\W\\r\\n]*end note| as (?<alias>\\w+)[.\\s\\S\\W\\r\\n]*end note)");
        private Regex parenthesies = new Regex("(\\(|\\))");

        public void FormatText(string text, FormattedText formattedText)
        {
            var mn = notes.Matches(text);

            foreach (var item in _mcolorCodes)
            {
                foreach (Match m in item.Key.Matches(text))
                {
                    formattedText.SetForegroundBrush(new SolidColorBrush(item.Value.Item1), m.Index, m.Length);
                    if (item.Value.Item2)
                    {
                        formattedText.SetFontStyle(FontStyles.Italic, m.Index, m.Length);
                    }
                }
            }
            foreach (var item in _colorCodes)
            {
                foreach (Match m in item.Key.Matches(text))
                {
                    var g = m.Groups[m.Groups.Count - 1];
                    formattedText.SetForegroundBrush(new SolidColorBrush(item.Value), g.Index, g.Length);
                }
            }
            foreach (Match m in mn)
            {
                formattedText.SetForegroundBrush(Brushes.Gray, m.Index, m.Length);
            }

            foreach (Match m in parenthesies.Matches(text))
            {
                formattedText.SetForegroundBrush(Brushes.Black, m.Index, m.Length);
                formattedText.SetFontWeight(FontWeights.Bold, m.Index, m.Length);
            }
            foreach (Match m in brackets.Matches(text))
            {
                formattedText.SetForegroundBrush(Brushes.Green, m.Index, m.Length);
                formattedText.SetFontWeight(FontWeights.Bold, m.Index, m.Length);
            }
        }
    }
}