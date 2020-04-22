using NUnit.Framework;
using PlantUML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UMLModels;

namespace UmlTests
{
    internal enum WindowSize
    {
        FULL = 1,
        LARGE = 2,
        MEDIUM = 4,
        SMALL = 8
    }

    internal enum W2
    {
        Other = 16,
        more = 32
    }

    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public async Task SequenceDiagramRead2()
        {
            var bidMask = Convert.ToInt32("18");

            var ss = bidMask & 0b_1111;
            var ss2 = bidMask & 0b_110000;

            var cacheWindowSize = ((IEnumerable<int>)Enum.GetValues(typeof(WindowSize))).Sum();
            var cacheW2 = ((IEnumerable<int>)Enum.GetValues(typeof(W2))).Sum();

            var sss = bidMask & cacheWindowSize;
            var sss2 = bidMask & cacheW2;

            var dgg = Enum.ToObject(typeof(WindowSize), bidMask);
            var ggg = Enum.ToObject(typeof(W2), bidMask);

            UMLInterface i1 = new UMLInterface("ns1", "i1");
            UMLClass class2 = new UMLClass("ns2", true, "c1", null, i1);

            i1.Methods.Add(new UMLMethod("Method1", new VoidDataType(), UMLVisibility.Public, new UMLParameter("parm1", new StringDataType())));
            class2.Methods.Add(new UMLMethod(class2, UMLVisibility.Public));

            class2.Methods.Add(new UMLMethod("Method1", new IntDataType(), UMLVisibility.Public, new UMLParameter("parm1", new StringDataType())));

            UMLClassDiagram cd = new UMLClassDiagram("a", "");
            cd.DataTypes.Add(i1);
            cd.DataTypes.Add(class2);

            UMLSequenceDiagram d = new UMLSequenceDiagram("my diagram", "");

            var l1 = new UMLSequenceLifeline("i1", "ss", i1.Id);
            var l2 = new UMLSequenceLifeline("c1", "cS", class2.Id);

            d.LifeLines.Add(l1);
            d.LifeLines.Add(l2);

            d.AddConnection(null, l1).Action = i1.Methods[0];

            var pg = new UMLSequenceBlockSection("if z = 1", UMLSequenceBlockSection.SectionTypes.If);

            d.Entities.Add(pg);

            pg.AddConnection(l1, l2).Action = class2.Methods[0];

            var elsesection = new UMLSequenceBlockSection("else", UMLSequenceBlockSection.SectionTypes.Else);

            elsesection.AddConnection(l2, l2).Action = class2.Methods[1];

            var x = d.AddConnection(l2, l1);
            x.Action = new UMLReturnFromMethod(class2.Methods[0]);

            var x2 = d.AddConnection(l1, l1);
            x2.Action = i1.Methods[0];

            d.AddConnection(l1, null).Action = new UMLReturnFromMethod(i1.Methods[0]);

            PlantUMLGenerator p = new PlantUMLGenerator();
            var s = p.Create(d);

            var sq = await UMLSequenceDiagramParser.ReadString(s, cd.DataTypes.ToDictionary(p => p.Id, p => p), false);
        }

        [Test]
        public void SequenceDiagram2()
        {
            UMLInterface i1 = new UMLInterface("ns1", "i1");
            UMLClass class2 = new UMLClass("ns2", true, "c1", null, i1);

            i1.Methods.Add(new UMLMethod("Method1", new VoidDataType(), UMLVisibility.Public,  new UMLParameter("parm1", new StringDataType())));
            class2.Methods.Add(new UMLMethod(class2, UMLVisibility.Public));

            class2.Methods.Add(new UMLMethod("Method1", new IntDataType(), UMLVisibility.Public, new UMLParameter("parm1", new StringDataType())));

            UMLSequenceDiagram d = new UMLSequenceDiagram("my diagram", "");

            var l1 = new UMLSequenceLifeline("ddd", "ddd", i1.Id);
            var l2 = new UMLSequenceLifeline("c2", "eee", class2.Id);

            d.LifeLines.Add(l1);
            d.LifeLines.Add(l2);

            d.AddConnection(null, l1).Action = i1.Methods[0];

            var pg = new UMLSequenceBlockSection("if z = 1", UMLSequenceBlockSection.SectionTypes.If);

            d.Entities.Add(pg);

            pg.AddConnection(l1, l2).Action = class2.Methods[0];

            var elsesection = new UMLSequenceBlockSection("else", UMLSequenceBlockSection.SectionTypes.Else);

            elsesection.AddConnection(l2, l2).Action = class2.Methods[1];

            var x = d.AddConnection(l2, l1);
            x.Action = new UMLReturnFromMethod(class2.Methods[0]);

            var x2 = d.AddConnection(l1, l1);
            x2.Action = i1.Methods[0];

            d.AddConnection(l1, null).Action = new UMLReturnFromMethod(i1.Methods[0]);

            PlantUMLGenerator p = new PlantUMLGenerator();
            var s = p.Create(d);
        }

        [Test]
        public void SequenceDiagram1()
        {
            UMLInterface i1 = new UMLInterface("ns1", "i1");
            UMLClass class2 = new UMLClass("ns2", true, "c1", null, i1);

            i1.Methods.Add(new UMLMethod("Method1", new VoidDataType(), UMLVisibility.Public, new UMLParameter("parm1", new StringDataType())));
            class2.Methods.Add(new UMLMethod(class2, UMLVisibility.Public));

            class2.Methods.Add(new UMLMethod("Method1", new IntDataType(), UMLVisibility.Public, new UMLParameter("parm1", new StringDataType())));

            UMLSequenceDiagram d = new UMLSequenceDiagram("my diagram", "");

            var l1 = new UMLSequenceLifeline("i1", "i1a", i1.Id);
            var l2 = new UMLSequenceLifeline("c2", "c2a", class2.Id);

            d.LifeLines.Add(l1);
            d.LifeLines.Add(l2);

            d.AddConnection(null, l1).Action = i1.Methods[0];
            d.AddConnection(l1, l2).Action = class2.Methods[0];
            d.AddConnection(l2, l1).Action = new UMLReturnFromMethod(class2.Methods[0]);
            d.AddConnection(l1, null).Action = new UMLReturnFromMethod(i1.Methods[0]);

            PlantUMLGenerator p = new PlantUMLGenerator();
            var s = p.Create(d);
        }

        [Test]
        public void ClassDiagram1()
        {
            UMLInterface i1 = new UMLInterface("", "i1");
            UMLClass class2 = new UMLClass("ns", true, "c1", null, i1);

            class2.Properties.Add(new UMLProperty("prop1", i1, UMLVisibility.Public, ListTypes.List));

            i1.Methods.Add(new UMLMethod("Method1", new VoidDataType(), UMLVisibility.Public, new UMLParameter("parm1", new StringDataType())));
            class2.Methods.Add(new UMLMethod(class2, UMLVisibility.Public));

            class2.Methods.Add(new UMLMethod("Method1", new IntDataType(), UMLVisibility.Public, new UMLParameter("parm1", new StringDataType())));

            var d = new UMLClassDiagram("test", "");
            d.DataTypes.Add(i1);
            d.DataTypes.Add(class2);

            PlantUMLGenerator p = new PlantUMLGenerator();
            var s = p.Create(d);
        }

        [Test]
        public async Task ClassDiagramRead1()
        {
            UMLInterface i1 = new UMLInterface("", "i1");
            UMLClass class2 = new UMLClass("ns", true, "c1", null, i1);

            class2.Properties.Add(new UMLProperty("prop1", i1, UMLVisibility.Public, ListTypes.List));

            i1.Methods.Add(new UMLMethod("Method1", new VoidDataType(), UMLVisibility.Public, new UMLParameter("parm1", new StringDataType()),
                 new UMLParameter("parm2", new BoolDataType()),
                   new UMLParameter("parm3", new StringDataType(), ListTypes.IReadOnlyCollection)));
            class2.Methods.Add(new UMLMethod(class2, UMLVisibility.Public));

            class2.Methods.Add(new UMLMethod("Method1", new IntDataType(), UMLVisibility.Public, new UMLParameter("parm1", new StringDataType())));

            var d = new UMLClassDiagram("test", "");
            d.DataTypes.Add(i1);
            d.DataTypes.Add(class2);

            PlantUMLGenerator p = new PlantUMLGenerator();
            var s = p.Create(d);

            var gg = await UMLClassDiagramParser.ReadClassDiagramString(s);
        }
    }
}