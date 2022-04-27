using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    public class UMLColorCoding : IColorCodingProvider
    {
        private static readonly Dictionary<Regex, Color> _colorCodes = new()
        {
            {
                new Regex("(@start\\w+|@end\\w+)", RegexOptions.Compiled),
                Colors.Coral
            },
            {
                new Regex(@"^\s*['!].+", RegexOptions.Multiline),
                Colors.Gray
            },
            {
                new Regex(@"^\s*(class|interface|abstract +class)\s+.+?{([\s.\w\W]+?)}", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled),
                Colors.Firebrick
            },
            {
                new Regex(@"^\s*(\*+|abstract class|title|class|\{\w+\}|usecase|interface|activate|deactivate|package|together|alt|opt|loop|try|group|catch|break|par|end|enum|participant|actor|control|component|database|boundary|queue|entity|collections|else|rectangle|queue|node|folder|cloud)\s+?", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Colors.Blue
            },
            {
                new Regex(@"^\s*(start|endif|if\s+\(.*|else\s+\(.*|repeat\s+while\s+\(.*|repeat|end\s+fork|fork\s+again|fork)\s+?", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Colors.Blue
            }

        };

        private static readonly Dictionary<Regex, Color[]> _groupedCodes = new()
        {
            {
                new Regex(@"^\s*(?<k>package|rectangle|usecase|folder|participant|cloud|folder|actor|database|queue|component|class|interface|enum|boundary|entity)\s+(?:.+?)\s+(?<k>as)\s+(?:[\w\s\{]+?)$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Color[] { Colors.Blue, Colors.Green }
            }
        };

        private static readonly Dictionary<Regex, (Color, bool)> _mcolorCodes = new()
        {
            {
                new Regex(@"((?<!\b(component|folder|package)\b.+)\:.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                (Colors.Firebrick, false)
            },
            {
                new Regex(@"^\s*(?:alt|opt|loop|try|group|catch|break|par|end|else) +?(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled),
                (Colors.Firebrick, false)
            }
        };

        private static readonly Regex brackets = new(@"(\{|\})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex notes = new(@"note +(?:(?<sl>(?<placement>\w+)( +of +| +)(?<target>.+) *: *(?<text>.*))|(?<sl>(?<placement>\w+) *: *(?<text>.*))|(?<sl>\""(?<text>[\w\W]+)\"" +as +(?<alias>\w+))|(?<sl>(?<placement>\w+)( +of +| +)(?<target>.+?\n)(?<text>[.\s\S\W]*?)end note)|(?<sl>as +(?<alias>[\w]+?)\n(?<text>[.\s\S\W]*?)end note))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex parenthesies = new(@"(\(|\))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public List<FormatResult> FormatText(string text)
        {
            List<FormatResult> list = new();
            MatchCollection? mn = notes.Matches(text);

            foreach (KeyValuePair<Regex, (Color, bool)> item in _mcolorCodes)
            {
                foreach (Match m in item.Key.Matches(text))
                {
                    list.Add(new FormatResult(new SolidColorBrush(item.Value.Item1), m.Index, m.Length, FontWeights.Normal, m.Value));

                }
            }
            foreach (KeyValuePair<Regex, Color[]> item in _groupedCodes)
            {
                foreach (Match m in item.Key.Matches(text))
                {
                    Group? g = m.Groups["k"];

                    list.Add(new FormatResult(new SolidColorBrush(item.Value[0]), g.Captures[0].Index, g.Captures[0].Length, FontWeights.Normal, g.Captures[0].Value));
                    list.Add(new FormatResult(new SolidColorBrush(item.Value[1]), g.Captures[1].Index, g.Captures[1].Length, FontWeights.Normal, g.Captures[1].Value));


                }
            }
            foreach (KeyValuePair<Regex, Color> item in _colorCodes)
            {
                foreach (Match m in item.Key.Matches(text))
                {
                    Group? g = m.Groups[^1];
                    list.Add(new FormatResult(new SolidColorBrush(item.Value), g.Index, g.Length, FontWeights.Normal, g.Value));

                }
            }


            foreach (Match m in parenthesies.Matches(text))
            {
                list.Add(new FormatResult(Brushes.Black, m.Index, m.Length, FontWeights.Bold, m.Value));

            }
            foreach (Match m in brackets.Matches(text))
            {
                list.Add(new FormatResult(Brushes.Green, m.Index, m.Length, FontWeights.Bold, m.Value));

            }

            foreach (Match m in mn)
            {
                list.Add(new FormatResult(Brushes.Gray, m.Index, m.Length, FontWeights.Normal, m.Value));

            }

            return list;
        }
    }
}