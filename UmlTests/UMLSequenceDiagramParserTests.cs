using NUnit.Framework;
using PlantUML;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UMLModels;

namespace UmlTests
{
    [TestFixture]
    public class UMLSequenceDiagramParserTests
    {
        private UMLClassDiagram _classDiagram;
        private LockedList<UMLClassDiagram> _classDiagrams;

        [SetUp]
        public void SetUp()
        {
            _classDiagram = new UMLClassDiagram("Test", "");

      
            var iUser = new UMLInterface("ns", "IUser", string.Empty, new List<UMLDataType>());
            var user = new UMLClass("ns", "User", string.Empty, true, "User", null);
            var auth = new UMLClass("ns", "AuthService", string.Empty, true, "AuthService", null);

            iUser.Methods.Add(new UMLMethod("GetInfo", new StringDataType(), UMLVisibility.Public));
            iUser.Methods.Add(new UMLMethod("UpdateProfile", new VoidDataType(), UMLVisibility.Public, new UMLParameter("name", new StringDataType())));

            user.Methods.Add(new UMLMethod("GetInfo", new StringDataType(), UMLVisibility.Public));
            user.Methods.Add(new UMLMethod("Delete", new VoidDataType(), UMLVisibility.Public));

            auth.Methods.Add(new UMLMethod("Authenticate", new BoolDataType(), UMLVisibility.Public, new UMLParameter("user", new StringDataType()), new UMLParameter("pass", new StringDataType())));

            _classDiagram.Package.Children.Add(iUser);
            _classDiagram.Package.Children.Add(user);
            _classDiagram.Package.Children.Add(auth);

            _classDiagrams = new LockedList<UMLClassDiagram>(new[] { _classDiagram });
        }

        [Test]
        public async Task ParseSimpleParticipants()
        {
            string s = """
            @startuml
            participant Alice
            actor Bob as B
            control C as Controller
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);

            Assert.That(d, Is.Not.Null);
            Assert.That(d.LifeLines.Count, Is.EqualTo(3));
            Assert.That(d.LifeLines.Any(l => l.Text == "Alice"));
            Assert.That(d.LifeLines.Any(l => l.Alias == "B"));
            Assert.That(d.LifeLines.Any(l => l.Alias == "Controller"));
        }

        [Test]
        public async Task ParseAllLifelineTypes()
        {
            // Types supported by regex: participant|create|actor|control|component|database|boundary|entity|collections
            string s = "@startuml\n" +
                       "participant P as P1\n" +
                       "create C as C1\n" +
                       "actor A as A1\n" +
                       "control Ctrl as Ctrl1\n" +
                       "component Comp as Comp1\n" +
                       "database DB as DB1\n" +
                       "boundary B as B1\n" +
                       "entity E as E1\n" +
                       "collections Col as Col1\n" +
                       "@enduml\n";

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);

            Assert.That(d, Is.Not.Null);
            var types = new Dictionary<string, string>
            {
                { "P1", "participant" },
                { "C1", "create" },
                { "A1", "actor" },
                { "Ctrl1", "control" },
                { "Comp1", "component" },
                { "DB1", "database" },
                { "B1", "boundary" },
                { "E1", "entity" },
                { "Col1", "collections" }
            };

            foreach (var kv in types)
            {
                var lf = d.LifeLines.FirstOrDefault(l => l.Alias == kv.Key);
                Assert.That(lf, Is.Not.Null, $"Expected lifeline with alias {kv.Key}");
                Assert.That(lf.LifeLineType, Is.EqualTo(kv.Value), $"Type mismatch for {kv.Key}");
            }
        }

        [Test]
        public async Task ParseLifelineWithQuotedNameAndAlias()
        {
            string s = """
            @startuml
            participant "Web Browser" as WB
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);

            Assert.That(d.LifeLines.Count, Is.EqualTo(1));
            Assert.That(d.LifeLines[0].Text, Is.EqualTo("Web Browser"));
            Assert.That(d.LifeLines[0].Alias, Is.EqualTo("WB"));
        }

        [Test]
        public async Task ParseBasicMessages()
        {
            string s = """
            @startuml
            participant A
            participant B
            A -> B: Hello()
            B --> A: return
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);

            Assert.That(d.Entities.Count, Is.GreaterThanOrEqualTo(1));
            var first = d.Entities.OfType<UMLSequenceConnection>().FirstOrDefault();
            Assert.That(first, Is.Not.Null);
            Assert.That(first.From, Is.Not.Null);
            Assert.That(first.To, Is.Not.Null);
            Assert.That(first.Action, Is.Not.Null);
        }

        [Test]
        public async Task ParseSelfMessage()
        {
            string s = """
            @startuml
            participant A
            A -> A: Refresh()
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);
            var conn = d.Entities.OfType<UMLSequenceConnection>().FirstOrDefault();
            Assert.That(conn, Is.Not.Null);
            Assert.That(conn.From, Is.EqualTo(conn.To));
        }

        [Test]
        public async Task ParseCreateAndCustomAction()
        {
            string s = """
            @startuml
            participant S
            participant U
            S -> U: <<create>> NewUser()
            S -> U: "custom label"
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);
            var actions = d.Entities.OfType<UMLSequenceConnection>().Select(c => c.Action).ToList();

            Assert.That(actions, Has.Exactly(1).Matches<UMLSignature>(a => a is UMLCreateAction));
            Assert.That(actions, Has.Exactly(1).Matches<UMLSignature>(a => a is UMLCustomAction));
        }

        [Test]
        public async Task ParseArrowVariants()
        {
            string[] variants = new[]
            {
                "A -> B: m",
                "A --> B: m",
                "A ->> B: m",
                "A -->> B: m",
                "A -[#red]> B: m",
                "A -left-> B: m",
                "A <- B: m",
                "A <-- B: m",
                "<- A: m",
                "-> A: m",
                "A <- B",
                "A -> B"
            };

            foreach (var v in variants)
            {
                string s = "@startuml\nparticipant A\nparticipant B\n" + v + "\n@enduml\n";
                var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);
                Assert.That(d, Is.Not.Null, "Failed to parse variant: " + v);
            }
        }

        [Test]
        public async Task ParseBlocks_AltLoopParRefCatchFinallyCriticalGroup()
        {
            string s = """
            @startuml
            participant A
            participant B
            alt condition
            A -> B: Msg1
            else
            A -> B: Msg2
            end
            loop 10 times
            A -> B: LoopMsg
            end
            par parallel
            A -> B: P1
            else
            A -> B: P2
            end
            try operation
            A -> B: Do
            catch ex
            A -> B: Handle
            finally
            A -> B: Cleanup
            end
            ref over A,B: reference
            critical section
            A -> B: CriticalMessage
            end
            group MyGroup
            A -> B: grouped
            end
            break
            A -> B: breakmsg
            end
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);

            Assert.That(d.Entities.OfType<UMLSequenceBlockSection>().Any(), Is.True);
            // ensure several section types are present
            var types = d.Entities.OfType<UMLSequenceBlockSection>().Select(b => b.SectionType).ToList();
            Assert.That(d.LineErrors, Is.Empty );
            Assert.That(types, Has.Some.EqualTo(UMLSequenceBlockSection.SectionTypes.If));
            Assert.That(types, Has.Some.EqualTo(UMLSequenceBlockSection.SectionTypes.Loop));
            Assert.That(types, Has.Some.EqualTo(UMLSequenceBlockSection.SectionTypes.Parrallel));
            Assert.That(types, Has.Some.EqualTo(UMLSequenceBlockSection.SectionTypes.Try));
            Assert.That(types, Has.Some.EqualTo(UMLSequenceBlockSection.SectionTypes.Catch));
            Assert.That(types, Has.Some.EqualTo(UMLSequenceBlockSection.SectionTypes.Finally));
            Assert.That(types, Has.Some.EqualTo(UMLSequenceBlockSection.SectionTypes.Group));
            Assert.That(types, Has.Some.EqualTo(UMLSequenceBlockSection.SectionTypes.Critical));
            Assert.That(types, Has.Some.EqualTo(UMLSequenceBlockSection.SectionTypes.Break));
        }

        [Test]
        public async Task ParseActivateDeactivateDestroy()
        {
            string s = """
            @startuml
            participant A
            participant B
            A -> B: Call
            activate B
            B -> B: DoWork
            deactivate B
            destroy B
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);
            Assert.That(d.Entities.Count, Is.GreaterThan(0));
            Assert.That(d.Entities.Any(e => e is UMLSequenceOther));
        }

        [Test]
        public async Task ParseNotesVarious()
        {
            string s = """
            @startuml
            participant A
            participant B
            note right of A: This is a note
            note left of B: left note
            note over A,B: over note
            A -> B: Message
            note on link: link note
            end note
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);
            Assert.That(d, Is.Not.Null);
        }

        [Test]
        public async Task ParseDirectives_Title_Novalidate_LaxMode_Autonumber_HeaderFooter()
        {
            string s = """
            @startuml
            title My Title
            '@@novalidate
            '@@laxmode
            autonumber
            header MyHeader
            footer MyFooter
            newpage
            participant A
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);
            Assert.That(d.Title, Does.Contain("My Title"));
            Assert.That(d.ValidateAgainstClasses, Is.False);
            Assert.That(d.LaxMode, Is.True);
        }

        [Test]
        public async Task ParseReturnFromPreviousAndLifelineReturn()
        {
            string s = """
            @startuml
            participant A
            participant B
            A -> B: DoWork()
            B --> A: return
            participant User
            B --> A: return User
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);
            var conns = d.Entities.OfType<UMLSequenceConnection>().ToList();
            // second connection should be return from previous (UMLReturnFromMethod)
            Assert.That(conns.Count >= 2);
            Assert.That(conns[1].Action, Is.TypeOf<UMLReturnFromMethod>());
            // later return referencing lifeline name should map to UMLLifelineReturnAction
            var lifelineReturn = conns.LastOrDefault(c => c.Action is UMLLifelineReturnAction);
            Assert.That(lifelineReturn, Is.Not.Null);
        }

        [Test]
        public async Task ParseFromEmptyAndToEmpty()
        {
            string s = """
            @startuml
            participant A
            -> A: ExternalTrigger
            A -> : ExternalResult
            <- A: ExternalReturn
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);
            var conns = d.Entities.OfType<UMLSequenceConnection>().ToList();
            Assert.That(conns.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task ParseGenericLifelineAndQuotedAlias()
        {
            string s = """
            @startuml
            participant List<User> as LU
            participant MS as "MS"
            LU -> "MS": Call()
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);
            Assert.That(d.LifeLines.Count, Is.EqualTo(2));
            Assert.That(d.LifeLines.Any(l => l.Alias == "LU"));
            Assert.That(d.LifeLines.Any(l => l.Alias == "MS"));
        }

        [Test]
        public async Task UnknownActionGeneratesWarningWhenValidated()
        {
            // create lifeline that maps to a known data type (User)
            var sd = new UMLSequenceDiagram("t", "");
            var lu = new UMLSequenceLifeline("participant", "User", "U", _classDiagram.DataTypes[1].Id, 1);
            var lb = new UMLSequenceLifeline("participant", "AuthService", "AS", _classDiagram.DataTypes[2].Id, 2);
            sd.LifeLines.Add(lu);
            sd.LifeLines.Add(lb);

            UMLSequenceConnection? r = null;

            // craft message with unknown action
            bool ok = UMLSequenceDiagramParser.TryParseAllConnections("U -> AS: UnknownMethod()", sd,
                _classDiagrams.SelectMany(x => x.DataTypes).ToLookup(t => t.Name),     r, 1, out var conn);
            Assert.That(ok, Is.True);
            Assert.That(conn, Is.Not.Null);
            Assert.That(conn.Action, Is.TypeOf<UMLUnknownAction>());
            // warning should be non-null because To is not free-formed and action unknown
            Assert.That(conn.Warning, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void TryParseAllConnections_InvalidSyntaxReturnsFalse()
        {
            var d = new UMLSequenceDiagram("t", "");
            string line = "this is not a connection";
            var types = _classDiagrams.SelectMany(x => x.DataTypes).ToLookup(t => t.Name);

            UMLSequenceConnection? r = null;
            bool ok = UMLSequenceDiagramParser.TryParseAllConnections(line, d, types,   r, 1, out var conn);
            Assert.That(ok, Is.False);
            Assert.That(conn, Is.Null);
        }

        [Test]
        public async Task ParseAllCommonParsingLines()
        {
            string s = """
            @startuml
            ' single line comment
            /' multiline comment start
            inside comment
            '/
            [hidden] something [hidden]
            left to right direction
            show footbox
            remove footbox
            hide stereotype
            scale 2
            !define TEST
            skinparam backgroundColor #EEE
            skinparam {
              a
            }
            legend
            legend line
            endlegend
            note "short note" as N
            note right of A: simple note
            autonumber
            header h
            footer f
            newpage
            ...
            ||
            autoactivate
            return
            box Box1
            end box
            == divider ==
            @enduml
            """;

            var d = await UMLSequenceDiagramParser.ReadString(s, _classDiagrams, false);
            Assert.That(d, Is.Not.Null);
            // Should not produce errors for these common lines
            Assert.That(d.LineErrors, Is.Empty);
        }

        [Test]
        public async Task ParseRoundtrip_GenerateAndRead()
        {
            var sd = new UMLSequenceDiagram("round", "");
            var l1 = new UMLSequenceLifeline("participant", "User", "U", _classDiagram.DataTypes[1].Id, 1);
            var l2 = new UMLSequenceLifeline("participant", "AuthService", "AS", _classDiagram.DataTypes[2].Id, 2);
            sd.LifeLines.Add(l1);
            sd.LifeLines.Add(l2);
            sd.AddConnection(l1, l2, 3).Action = _classDiagram.DataTypes[2].Methods.FirstOrDefault();

            string plant = PlantUMLGenerator.Create(sd);
            var parsed = await UMLSequenceDiagramParser.ReadString(plant, new LockedList<UMLClassDiagram>(new[] { _classDiagram }), false);
            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.LifeLines.Count, Is.GreaterThanOrEqualTo(2));
        }
    }
}
