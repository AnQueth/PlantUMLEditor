// See https://aka.ms/new-console-template for more information
using System.Text;


Parser parser = new Parser();

parser.QuoteParse("\"a b\" -> b : la", (s) =>
{
    Console.WriteLine(s);
});

LinePointerParser lp = new();
parser.QuoteParse("a -> b : la", (s) =>
{

    lp.Parse(s);

});


lp = new();
parser.QuoteParse("\"a b\" -> \"b aaaa\" : la", (s) =>
{
    lp.Parse(s);
});

lp = new();
parser.QuoteParse(" -> \"b aaaa\" : la", (s) =>
{
    lp.Parse(s);
});

Console.ReadLine();

class LinePointerParser
{

    public string LeftSide;
    public string RightSide;
    public string Connector;
    public StringBuilder Text = new();

    public void Parse(string word)
    {
        if (word[0] is '-' or '<' or '>')
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

                Text.Append(word);
                Text.Append(' ');
            }
        }


    }
}

class Parser
{



    public void QuoteParse(string line, Action<string> wordReader, char quoteChar = '\"')
    {
        StringBuilder sb = new StringBuilder();


        bool inQuotes = false;
        for (var x = 0; x < line.Length; x++)
        {
            var c = line[x];

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