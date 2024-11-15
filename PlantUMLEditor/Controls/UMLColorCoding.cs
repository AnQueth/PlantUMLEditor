using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    public class UMLColorCoding : IColorCodingProvider
    {
        private static readonly Regex _brackets = new(@"(\{|\}|\[|\])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _comments = new Regex(@"^\s*((?:/'[\w\W]*?'/)|(?:'.+))", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex _notes = new(@"^\s*\/*\s*(note|hnote|rnote)((?:.+\:\s+.+?)$|((?:[.\W\w]+?)(end note|endrnote|endhnote)))", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _notes2 = new(@"^\s*\/*\s*(note|hnote|rnote).+?as\s+[\w]+", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _parentheses = new(@"(\(|\))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly List<(Regex Pattern, Brush Brush, int GroupIndex, FontWeight FontWeight)> _colorCodes;
   

        private readonly List<(Regex Pattern, Brush[] Brushes)> _groupedCodes;

        private readonly List<(Regex Pattern, Brush Brush)> _singleGroupCodes;

        public UMLColorCoding( )
        {
     
            _colorCodes = new()
                {
                    (new Regex("(@start\\w+|@end\\w+)", RegexOptions.Compiled), new SolidColorBrush( UMLColorCodingConfig.StartEndColor), 0, FontWeights.Normal),
                    (new Regex(@"^left +to +right +direction\s*$", RegexOptions.Compiled | RegexOptions.Multiline), new SolidColorBrush( UMLColorCodingConfig.DirectionColor), 0, FontWeights.Normal),
                    (new Regex(@"^[\s\+\-\#]*(\*+|abstract class|\{static\}|\{abstract\}|show|remove|skinparam|box|end box|autonumber|hide|title|class|\{\w+\}|usecase|legend|endlegend|interface|activate|deactivate|package|together|alt(?:\#[\w]*)|alt|opt|loop|try|group|catch|break|par|end|enum|participant|actor|control|component|database|boundary|queue|entity|collections|else|rectangle|queue|node|folder|cloud)\s+?", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled), new SolidColorBrush( UMLColorCodingConfig.KeywordColor), 1, FontWeights.Normal),
                    (new Regex(@"^[\s\+\-\#]*(port|portin|portout)\s+?", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled), new SolidColorBrush( UMLColorCodingConfig.PortColor), 1, FontWeights.Normal),
                    (new Regex(@"^\s*(start|endif|if\s+\(.*|else\s+\(.*|repeat\s+while\s+\(.*|repeat|end\s+fork|fork\s+again|fork|while.*|endwhile.*)\s+?", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled),new SolidColorBrush(  UMLColorCodingConfig.ControlFlowColor), 1, FontWeights.Normal)
                 };

            _groupedCodes = new()
                {
                        (new Regex(@"^\s*(?<k>package|rectangle|usecase|folder|participant|cloud|folder|actor|database|queue|component|class|interface|enum|boundary|entity)\s+(?:.+?)\s+(?<k>as)\s+(?:.+?)$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled),
                        new Brush[] { new SolidColorBrush( UMLColorCodingConfig.KeywordColor), 
                          new SolidColorBrush(  UMLColorCodingConfig.GroupKeywordColor )})
                };

            _singleGroupCodes = new()
                {
                    (new Regex(@"(?<!\b(component|folder|package)\b.+)\s*\:(?(\s*\[\s*\n)()|(.+))", RegexOptions.Compiled | RegexOptions.IgnoreCase), 
                    new SolidColorBrush( UMLColorCodingConfig.SingleGroupColor))
                 };
        }

        public List<FormatResult> FormatText(string text)
        {
            List<FormatResult> list = new();

            foreach (var (pattern, brush) in _singleGroupCodes)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    var group = match.Groups[3];
                    list.Add(new FormatResult(brush, group.Index, group.Length, FontWeights.Normal, group.Value));
                }
            }

            foreach (var (pattern, brushes) in _groupedCodes)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    var group = match.Groups["k"];
                    list.Add(new FormatResult(brushes[0], group.Captures[0].Index, group.Captures[0].Length, FontWeights.Normal, group.Captures[0].Value));
                    list.Add(new FormatResult(brushes[1], group.Captures[1].Index, group.Captures[1].Length, FontWeights.Normal, group.Captures[1].Value));
                }
            }

            foreach (var (pattern, brush, groupIndex, fontWeight) in _colorCodes)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    var group = match.Groups[groupIndex];
                    list.Add(new FormatResult(brush, group.Index, group.Length, fontWeight, group.Value));
                }
            }

            foreach (Match match in _parentheses.Matches(text))
            {
                list.Add(new FormatResult(new SolidColorBrush( UMLColorCodingConfig.ParenthesisColor)
                    , match.Index, match.Length, FontWeights.Bold, match.Value));
            }

            foreach (Match match in _brackets.Matches(text))
            {
                list.Add(new FormatResult(new SolidColorBrush(UMLColorCodingConfig.BracketColor), match.Index, match.Length, FontWeights.Bold, match.Value));
            }

            foreach (Match match in _notes.Matches(text))
            {
                list.Add(new FormatResult(new SolidColorBrush(UMLColorCodingConfig.NoteColor), match.Index, match.Length, FontWeights.Normal, match.Value));
            }

            foreach (Match match in _notes2.Matches(text))
            {
                list.Add(new FormatResult(new SolidColorBrush(UMLColorCodingConfig.NoteColor), match.Index, match.Length, FontWeights.Normal, match.Value));
            }

            foreach (Match match in _comments.Matches(text))
            {
                list.Add(new FormatResult(new SolidColorBrush(UMLColorCodingConfig.CommentColor), match.Index, match.Length, FontWeights.Normal, match.Value));
            }

            return list;
        }
    }
}