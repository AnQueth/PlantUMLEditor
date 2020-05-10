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
            {new Regex("(\\{|\\})") , Colors.Purple},
             {new Regex("@startuml|@enduml") , Colors.Gray},
                {new Regex("^\\s*(class|\\{\\w+\\}|interface|package|alt|opt|loop|try|group|catch|break|par|participant|actor|database|component|end)\\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase), Colors.Blue}
        }; private Dictionary<Regex, (Color, bool)> _mcolorCodes = new Dictionary<Regex, (Color, bool)>()

        {
            {new Regex("^\\s*(participant|actor|database|component|class|interface)\\s+\\w+\\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase), (Colors.Green, false ) },
              {new Regex("title.*"), (Colors.Green, false ) },
               {new Regex("(\\:.+)"), (Colors.DarkBlue, true) }
        };

        private Regex notes = new Regex("note *((?<sl>(?<placement>\\w+) of (?<target>\\w+) *: *(?<text>.*))|(?<sl>(?<placement>\\w+) *: *(?<text>.*))|(?<sl>\\\"(?<text>[\\w\\W]+)\\\" as (?<alias>\\w+))|(?<placement>\\w+) of (?<target>\\w+)[.\\s\\S\\W\\r\\n]*end note| as (?<alias>\\w+)[.\\s\\S\\W\\r\\n]*end note)");

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
                    formattedText.SetForegroundBrush(new SolidColorBrush(item.Value), m.Index, m.Length);
                }
            }
            foreach (Match m in mn)
            {
                formattedText.SetForegroundBrush(Brushes.Red, m.Index, m.Length);
            }
        }
    }
}