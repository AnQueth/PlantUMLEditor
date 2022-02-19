using System.Collections.Generic;
using System.Text;

namespace Parsers
{
    public class KeyWordStartingParser
    {
        private readonly HashSet<string> _keywords;
        private bool _finishedKeywords = false;
        private readonly List<string> _matchedWords = new();
        private readonly List<string> _leftOvers = new();

        public List<string> MatchedKeywords => _matchedWords;

        public List<string> LeftOvers => _leftOvers;

        public string LeftOverToString()
        {
            StringBuilder sb = new StringBuilder();
            for (var a = 0; a < _leftOvers.Count; a++)
            {

                sb.Append(_leftOvers[a]);
                if (a != _leftOvers.Count - 1)
                {
                    sb.Append(' ');
                }
            }
            return sb.ToString();
        }

        public KeyWordStartingParser(params string[] keywords)
        {
            _keywords = new(keywords);
        }

        public void Parse(string word)
        {
            if (_finishedKeywords is false)
            {
                if (_keywords.Contains(word))
                {

                    _matchedWords.Add(word);
                    return;
                }
                else
                {
                    _finishedKeywords = true;
                }
            }

            _leftOvers.Add(word);
        }
    }
}
