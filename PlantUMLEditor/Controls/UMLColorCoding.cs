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

        // Cached brushes to avoid allocations on each FormatText call
        private readonly Brush _parenthesisBrush;
        private readonly Brush _bracketBrush;
        private readonly Brush _noteBrush;
        private readonly Brush _commentBrush;

        public UMLColorCoding( )
        {
            // create and freeze brushes to reduce allocation and improve rendering performance
            Brush CreateFrozen(Color c)
            {
                var b = new SolidColorBrush(c);
                if (b.CanFreeze)
                {
                    b.Freeze();
                }
                return b;
            }

            _parenthesisBrush = CreateFrozen(UMLColorCodingConfig.ParenthesisColor);
            _bracketBrush = CreateFrozen(UMLColorCodingConfig.BracketColor);
            _noteBrush = CreateFrozen(UMLColorCodingConfig.NoteColor);
            _commentBrush = CreateFrozen(UMLColorCodingConfig.CommentColor);

            _colorCodes = new()
                {
                    (new Regex("(@start\\w+|@end\\w+)", RegexOptions.Compiled), CreateFrozen(UMLColorCodingConfig.StartEndColor), 0, FontWeights.Normal),
                    (new Regex(@"^left +to +right +direction\s*$", RegexOptions.Compiled | RegexOptions.Multiline), CreateFrozen( UMLColorCodingConfig.DirectionColor), 0, FontWeights.Normal),
                    (new Regex(@"^[\s\+\-\#]*(\*+|abstract class|\{static\}|\{abstract\}|show|remove|skinparam|box|end box|autonumber|hide|title|class|\{\w+\}|usecase|legend|endlegend|interface|struct|activate|deactivate|package|together|alt(?:\#[\w]*)|alt|opt|loop|try|group|catch|break|par|end|enum|participant|actor|control|component|database|boundary|queue|entity|collections|else|rectangle|queue|node|folder|cloud)\s+?", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled), CreateFrozen( UMLColorCodingConfig.KeywordColor), 1, FontWeights.Normal),
                    (new Regex(@"^[\s\+\-\#]*(port|portin|portout)\s+?", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled), CreateFrozen( UMLColorCodingConfig.PortColor), 1, FontWeights.Normal),
                    (new Regex(@"^\s*(start|endif|if\s+\(.*|else\s+\(.*|repeat\s+while\s+\(.*|repeat|end\s+fork|fork\s+again|fork|while.*|endwhile.*)\s+?", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled),CreateFrozen(  UMLColorCodingConfig.ControlFlowColor), 1, FontWeights.Normal)
                 };

            _groupedCodes = new()
                {
                        (new Regex(@"^\s*(?<k>package|rectangle|usecase|folder|participant|cloud|folder|actor|database|queue|component|struct|class|interface|enum|boundary|entity)\s+(?:.+?)\s+(?<k>as)\s+(?:.+?)$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled),
                        new Brush[] { CreateFrozen( UMLColorCodingConfig.KeywordColor), 
                          CreateFrozen(  UMLColorCodingConfig.GroupKeywordColor )})
                };

            _singleGroupCodes = new()
                {
                    (new Regex(@"(?<!\b(component|folder|package)\b.+)\s*\:(?(\s*\[\s*\n)()|(.+))", RegexOptions.Compiled | RegexOptions.IgnoreCase), 
                    CreateFrozen( UMLColorCodingConfig.SingleGroupColor))
                 };
        }

        public List<FormatResult> FormatText(string text)
        {
            List<FormatResult> list = new();

            foreach (var (pattern, brush) in _singleGroupCodes)
            {
                for (Match match = pattern.Match(text); match.Success; match = match.NextMatch())
                {
                    var group = match.Groups[3];
                    list.Add(new FormatResult(brush, group.Index, group.Length, FontWeights.Normal, group.Value));
                }
            }

            foreach (var (pattern, brushes) in _groupedCodes)
            {
                for (Match match = pattern.Match(text); match.Success; match = match.NextMatch())
                {
                    var group = match.Groups["k"];
                    if (group.Captures.Count >= 2)
                    {
                        list.Add(new FormatResult(brushes[0], group.Captures[0].Index, group.Captures[0].Length, FontWeights.Normal, group.Captures[0].Value));
                        list.Add(new FormatResult(brushes[1], group.Captures[1].Index, group.Captures[1].Length, FontWeights.Normal, group.Captures[1].Value));
                    }
                }
            }

            foreach (var (pattern, brush, groupIndex, fontWeight) in _colorCodes)
            {
                for (Match match = pattern.Match(text); match.Success; match = match.NextMatch())
                {
                    var group = match.Groups[groupIndex];
                    list.Add(new FormatResult(brush, group.Index, group.Length, fontWeight, group.Value));
                }
            }

            for (Match match = _parentheses.Match(text); match.Success; match = match.NextMatch())
            {
                list.Add(new FormatResult(_parenthesisBrush, match.Index, match.Length, FontWeights.Bold, match.Value));
            }

            for (Match match = _brackets.Match(text); match.Success; match = match.NextMatch())
            {
                list.Add(new FormatResult(_bracketBrush, match.Index, match.Length, FontWeights.Bold, match.Value));
            }

            for (Match match = _notes.Match(text); match.Success; match = match.NextMatch())
            {
                list.Add(new FormatResult(_noteBrush, match.Index, match.Length, FontWeights.Normal, match.Value));
            }

            for (Match match = _notes2.Match(text); match.Success; match = match.NextMatch())
            {
                list.Add(new FormatResult(_noteBrush, match.Index, match.Length, FontWeights.Normal, match.Value));
            }

            for (Match match = _comments.Match(text); match.Success; match = match.NextMatch())
            {
                list.Add(new FormatResult(_commentBrush, match.Index, match.Length, FontWeights.Normal, match.Value));
            }

            return list;
        }
    }
}