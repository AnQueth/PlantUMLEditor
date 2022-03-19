using System;
using System.Text;

namespace Parsers
{
    public class QuoteParser
    {

        public static void Parse(string line, Action<string> wordReader, char quoteChar = '\"')
        {
            StringBuilder sb = new StringBuilder();


            bool inQuotes = false;
            for (int x = 0; x < line.Length; x++)
            {
                char c = line[x];

                if (c == quoteChar)
                {


                    inQuotes = !inQuotes;

                    continue;
                }

                if (inQuotes is false)
                {
                    if (c is ' ' && sb.Length > 0)
                    {
                        string s = sb.ToString();
                        sb.Clear();

                        wordReader(s);
                        continue;
                    }
                    else if (c is ' ')
                    {
                        continue;
                    }
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
            {
                string s = sb.ToString();

                wordReader(s);
            }
        }
    }
}
