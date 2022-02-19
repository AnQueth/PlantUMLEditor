using NUnit.Framework;
using Parsers;

namespace UmlTests
{
    public class ParserTests
    {
        [Test]
        public void QuoteTest()
        {
            string a = null, b = null, c = null;
            QuoteParser.Parse("\"a b\" -> \"b aaa  fff'\" : la", (s) =>
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

            Assert.AreEqual(a, "a b");
            Assert.AreEqual(b, "->");
            Assert.AreEqual(c, "b aaa  fff'");
        }


        [Test]
        public void KeywordTest()
        {
            KeyWordStartingParser k = new("class", "abstract");

            QuoteParser.Parse("class Test {", (s) =>
            {
                k.Parse(s);
            });

            Assert.AreEqual("class", k.MatchedKeywords[0]);
            Assert.AreEqual("Test", k.LeftOvers[0]);
            Assert.AreEqual("Test {", k.LeftOverToString());

            k = new("class", "abstract");

            QuoteParser.Parse("abstract class \"Test la\" as a {", (s) =>
            {
                k.Parse(s);
            });

            Assert.AreEqual("abstract", k.MatchedKeywords[0]);
            Assert.AreEqual("class", k.MatchedKeywords[1]);
            Assert.AreEqual("Test la", k.LeftOvers[0]);
            Assert.AreEqual("Test la as a {", k.LeftOverToString());

            k = new("note", "top", "bottom", "over", "of", "right", ":");

            QuoteParser.Parse("note over a : test", (s) =>
            {
                k.Parse(s);
            });

            Assert.AreEqual("note", k.MatchedKeywords[0]);
            Assert.AreEqual("over", k.MatchedKeywords[1]);
            Assert.AreEqual("a : test", k.LeftOverToString());

        }

        [Test]
        public void ParseTest1()
        {

            LinePointerParser lp = new();
            QuoteParser.Parse("\"a b\" -> b : la", (s) =>
            {
                lp.Parse(s);

            });
            Assert.AreEqual(lp.LeftSide, "a b");
            Assert.AreEqual(lp.RightSide, "b");
            Assert.AreEqual(lp.Connector, "->");
            Assert.AreEqual(lp.Text, "la");

            lp = new();
            QuoteParser.Parse("a --> b : la test", (s) =>
            {

                lp.Parse(s);

            });
            Assert.AreEqual(lp.LeftSide, "a");
            Assert.AreEqual(lp.RightSide, "b");
            Assert.AreEqual(lp.Connector, "-->");
            Assert.AreEqual(lp.Text, "la test");


            lp = new();
            QuoteParser.Parse("\"a b\" ..> \"b aaaa\" : la other words", (s) =>
            {
                lp.Parse(s);
            });

            Assert.AreEqual(lp.LeftSide, "a b");
            Assert.AreEqual(lp.RightSide, "b aaaa");
            Assert.AreEqual(lp.Connector, "..>");
            Assert.AreEqual(lp.Text, "la other words");


            lp = new();
            QuoteParser.Parse(" --> \"b aaaa\" : la", (s) =>
            {
                lp.Parse(s);
            });
            Assert.AreEqual(lp.LeftSide, null);
            Assert.AreEqual(lp.RightSide, "b aaaa");
            Assert.AreEqual(lp.Connector, "-->");
            Assert.AreEqual(lp.Text, "la");

            lp = new();
            QuoteParser.Parse(" <-- \"b aaaa\" : la", (s) =>
            {
                lp.Parse(s);
            });
            Assert.AreEqual(lp.LeftSide, null);
            Assert.AreEqual(lp.RightSide, "b aaaa");
            Assert.AreEqual(lp.Connector, "<--");
            Assert.AreEqual(lp.Text, "la");
        }
    }
}
