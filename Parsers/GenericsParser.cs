using System;
using System.Text;

namespace Parsers
{
    public class GenericsParser
    {

        public static void Parse(ReadOnlySpan<char> line, Action<string, int, int> wordReader)
        {
            StringBuilder sb = new StringBuilder();


            bool inQuotes = false;
            for (int x = 0; x < line.Length; x++)
            {
                char c = line[x];

                if (c == '<')
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

                        wordReader(s, x, line.Length);
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

                wordReader(s, line.Length, line.Length);
            }
        }
    }
}
