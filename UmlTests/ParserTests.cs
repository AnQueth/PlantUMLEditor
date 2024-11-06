using NUnit.Framework;
using NUnit.Framework.Legacy;
using Parsers;
using System.IO;

namespace UmlTests
{
    public class ParserTests
    {
        [Test]
        public void QuoteTest()
        {
            string a = null, b = null, c = null;
            QuoteParser.Parse("\"a b\" -> \"b aaa  fff'\" : la", (s, pos, len) =>
            {
                if (a is null)
                {
                    a = s;
                }
                else if (b is null)
                {
                    b = s;
                }
                else if (c is null)
                {
                    c = s;
                }
            });

            ClassicAssert.AreEqual(a, "a b");
            ClassicAssert.AreEqual(b, "->");
            ClassicAssert.AreEqual(c, "b aaa  fff'");
        }


        [Test]
        public void KeywordTest()
        {
            KeyWordStartingParser k = new("class", "abstract");

            QuoteParser.Parse("class Test {", (s, pos, len) =>
            {
                k.Parse(s);
            });

            ClassicAssert.AreEqual("class", k.MatchedKeywords[0]);
            ClassicAssert.AreEqual("Test", k.LeftOvers[0]);
            ClassicAssert.AreEqual("Test {", k.LeftOverToString());

            k = new("class", "abstract");

            QuoteParser.Parse("abstract class \"Test la\" as a {", (s, pos, len) =>
            {
                k.Parse(s);
            });

            ClassicAssert.AreEqual("abstract", k.MatchedKeywords[0]);
            ClassicAssert.AreEqual("class", k.MatchedKeywords[1]);
            ClassicAssert.AreEqual("Test la", k.LeftOvers[0]);
            ClassicAssert.AreEqual("Test la as a {", k.LeftOverToString());

            k = new("note", "top", "bottom", "over", "of", "right", ":");

            QuoteParser.Parse("note over a : test", (s, pos, len) =>
            {
                k.Parse(s);
            });

            ClassicAssert.AreEqual("note", k.MatchedKeywords[0]);
            ClassicAssert.AreEqual("over", k.MatchedKeywords[1]);
            ClassicAssert.AreEqual("a : test", k.LeftOverToString());


            k = new("note", "top", "bottom", "over", "of", "right", ":");

            QuoteParser.Parse("note \"over a\" as test", (s, pos, len) =>
            {
                k.Parse(s);
            });

            ClassicAssert.AreEqual("note", k.MatchedKeywords[0]);
            ClassicAssert.AreEqual("over", k.MatchedKeywords[1]);
            ClassicAssert.AreEqual("a : test", k.LeftOverToString());
        }

        [Test]
        public void ReadSkinParams()
        {
            string test1 = "skinparam color blue";
            string test2 = @"skinparam class {
    color blue
    background red
}
";


            SkinParamParser skp = new();

            StringReader sr = new StringReader(test1);
            string line;
            while ((line = sr.ReadLine()) is not null)
            {
                if (!skp.ReadLine(line))
                {
                    ClassicAssert.AreSame(test1, skp.ReadLines);
                }
            }
            skp = new();

            sr = new StringReader(test2);

            while ((line = sr.ReadLine()) is not null)
            {
                if (!skp.ReadLine(line))
                {
                     ClassicAssert.AreSame(test2, skp.ReadLines);
                }
            }
        }

        [Test]
        public void ParseTest1()
        {

            LinePointerParser lp = new();
            QuoteParser.Parse("\"a b\" -> b : la", (s, pos, len) =>
            {
                lp.Parse(s);

            });
            ClassicAssert.AreEqual(lp.LeftSide, "a b");
            ClassicAssert.AreEqual(lp.RightSide, "b");
            ClassicAssert.AreEqual(lp.Connector, "->");
            ClassicAssert.AreEqual(lp.Text, "la");

            lp = new();
            QuoteParser.Parse("a --> b : la test", (s, pos, len) =>
            {

                lp.Parse(s);

            });
            ClassicAssert.AreEqual(lp.LeftSide, "a");
            ClassicAssert.AreEqual(lp.RightSide, "b");
            ClassicAssert.AreEqual(lp.Connector, "-->");
            ClassicAssert.AreEqual(lp.Text, "la test");


            lp = new();
            QuoteParser.Parse("\"a b\" ..> \"b aaaa\" : la other words", (s, pos, len) =>
            {
                lp.Parse(s);
            });

            ClassicAssert.AreEqual(lp.LeftSide, "a b");
            ClassicAssert.AreEqual(lp.RightSide, "b aaaa");
            ClassicAssert.AreEqual(lp.Connector, "..>");
            ClassicAssert.AreEqual(lp.Text, "la other words");


            lp = new();
            QuoteParser.Parse(" --> \"b aaaa\" : la", (s, pos, len) =>
            {
                lp.Parse(s);
            });
            ClassicAssert.AreEqual(lp.LeftSide, null);
            ClassicAssert.AreEqual(lp.RightSide, "b aaaa");
            ClassicAssert.AreEqual(lp.Connector, "-->");
            ClassicAssert.AreEqual(lp.Text, "la");

            lp = new();
            QuoteParser.Parse(" <-- \"b aaaa\" : la", (s, pos, len) =>
            {
                lp.Parse(s);
            });
            ClassicAssert.AreEqual(lp.LeftSide, null);
            ClassicAssert.AreEqual(lp.RightSide, "b aaaa");
            ClassicAssert.AreEqual(lp.Connector, "<--");
            ClassicAssert.AreEqual(lp.Text, "la");
        }
    }
}
