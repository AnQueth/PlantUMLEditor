using System;
using System.Text;

namespace Parsers
{
    public class SkinParamParser : MultiLineParser
    {
        private readonly StringBuilder _readInformation = new();

        protected override bool Read(ReadOnlySpan<char> word, int pos, int len)
        {
            bool needMore = false;
            if (pos == len)
            {
                if (word[0] is '{')
                {
                    needMore = true;
                }
            }

            _readInformation.Append(word);
            if (pos == len)
            {
                _readInformation.AppendLine();
            }
            else
            {
                _readInformation.Append(' ');
            }

            return needMore;
        }

        public string ReadLines => _readInformation.ToString();

    }

    public abstract class MultiLineParser
    {
        public bool ReadLine(ReadOnlySpan<char> line)
        {

            bool needsMoreReading = false;

            QuoteParser.Parse(line, (word, pos, len) =>
            {
                var readMore = Read(word, pos, len);
                if (readMore)
                {
                    needsMoreReading = true;
                }
            });

            return needsMoreReading;
        }

        protected abstract bool Read(ReadOnlySpan<char> word, int pos, int len);
    }

    public class LinePointerParser
    {

        public string? LeftSide
        {
            get; private set;
        }
        public string? RightSide
        {
            get; private set;
        }
        public string? Connector
        {
            get; private set;
        }
        private string? _textHolder;
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
            if (word[0] is '-' or '<' or '>' or '.' or '[' or ']')
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
