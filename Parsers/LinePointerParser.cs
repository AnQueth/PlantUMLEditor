using System.Text;

namespace Parsers
{

    public class LinePointerParser
    {

        public string LeftSide
        {
            get; private set;
        }
        public string RightSide
        {
            get; private set;
        }
        public string Connector
        {
            get; private set;
        }
        private string _textHolder;
        private readonly StringBuilder _textSb = new();
        public string Text
        {
            get
            {
                if (string.IsNullOrEmpty(_textHolder))
                {
                    _textHolder = _textSb.ToString().Trim();
                }

                return _textHolder;
            }
        }

        public void Parse(string word)
        {
            if (word[0] is '-' or '<' or '>' or '.')
            {
                Connector = word;
            }
            else if (Connector is null)
            {
                LeftSide = word;
            }
            else if (Connector is not null && RightSide is null)
            {
                RightSide = word;
            }
            else if (RightSide is not null)
            {
                if (word[0] is not ':')
                {

                    _textSb.Append(word);
                    _textSb.Append(' ');
                }
            }


        }
    }

}
