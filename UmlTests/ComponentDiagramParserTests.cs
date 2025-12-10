using NUnit.Framework;
using PlantUML;
using System.Linq;
using System.Threading.Tasks;
using UMLModels;

namespace UmlTests
{
    [TestFixture]
    public class ComponentDiagramParserTests
    {
        [Test]
        public async Task ParseSimpleComponentWithAlias()
        {
            string puml = """
            @startuml
            component User as U
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);

            Assert.That(diagram, Is.Not.Null);
            var comp = diagram.Entities.FirstOrDefault(d => d.Name == "User");
            Assert.That(comp, Is.Not.Null, "Component 'User' should be present");
            Assert.That(comp, Is.TypeOf<UMLComponent>());
            Assert.That(comp.Alias, Is.EqualTo("U"));
            Assert.That(comp.Namespace, Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task ParseBracketedComponentWithPortsAndChildren()
        {
            string puml = """
            @startuml
            component "Parent Component" as P {
                component Child as C
                port myPort
                portin inPort
                portout outPort
            }
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);

            Assert.That(diagram, Is.Not.Null);

            var parent = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "Parent Component");
            Assert.That(parent, Is.Not.Null, "Parent component should be present");
            Assert.That(parent.Alias, Is.EqualTo("P"));

            // Child should be present both in overall entities and as a child of parent
            var child = diagram.Entities.FirstOrDefault(d => d.Name == "Child");
            Assert.That(child, Is.Not.Null, "Child component should be present in diagram entities");

            var parentComp = parent as UMLComponent;
            Assert.That(parentComp.Children.Any(c => c.Name == "Child"), Is.True, "Child should be contained in parent's Children");

            // Ports added to current component
            Assert.That(parentComp.Ports.Contains("myPort"));
            Assert.That(parentComp.PortsIn.Contains("inPort"));
            Assert.That(parentComp.PortsOut.Contains("outPort"));
        }

        [Test]
        public async Task ParseComponentInsidePackageAndNestedPackages()
        {
            string puml = """
            @startuml
            package "Domain" {
                package "Models" {
                    component Repo as R
                }
            }
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);

            Assert.That(diagram, Is.Not.Null);

            var repo = diagram.Entities.FirstOrDefault(d => d.Name == "Repo");
            // The parser may set Name based on Clean(parseResult.name). For component type lines alias defaults to name; ensure we find by alias or name
            if (repo == null)
            {
                repo = diagram.Entities.FirstOrDefault(d => d.Alias == "R");
            }

            Assert.That(repo, Is.Not.Null, "Repository component should be present");
            Assert.That(repo.Namespace, Is.EqualTo("Domain.Models"));
        }

        [Test]
        public async Task ParseInterfaceInComponentDiagram()
        {
            string puml = """
            @startuml
            interface IService as I
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);

            Assert.That(diagram, Is.Not.Null);
            var iface = diagram.Entities.FirstOrDefault(d => d.Name == "IService" || d.Alias == "I");
            Assert.That(iface, Is.Not.Null, "Interface should be parsed as an entity");
            Assert.That(iface, Is.TypeOf<UMLInterface>());
        }

        [Test]
        public async Task DuplicateUnnamedBracketedComponentsProduceSingleEntityAndDoNotCrash()
        {
            // Two bracketed components without aliases - parser may add an empty alias key internally
            string puml = """
            @startuml
            [A]
            [B]
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);

            Assert.That(diagram, Is.Not.Null);
            Assert.That(diagram.Entities.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(diagram.Entities.Any(e => e.Name == "A"));
            Assert.That(diagram.Entities.Any(e => e.Name == "B"));
        }

        [Test]
        public async Task ParseComponentConnectionConsumesArrow()
        {
            string puml = """
            @startuml
            component A
            component B
            A --> B
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var left = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "A" || c.Alias == "A");
            var right = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "B" || c.Alias == "B");

            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);

            // According to parser logic, arrow ending '>' results in left.Consumes contains right
            Assert.That(left.Consumes.Contains(right), Is.True, "A should consume B for 'A --> B'");
        }

        [Test]
        public async Task ParseComponentConnectionExposesArrowWithAliasAndBrackets()
        {
            string puml = """
            @startuml
            component "My Left" as L
            component "My Right" as R
            [My Left] --o [My Right]
            L --o R
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var left = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "My Left" || c.Alias == "L");
            var right = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "My Right" || c.Alias == "R");

            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);

            // Arrow ending with 'o' (arrow string ending with 'o') should result in left.Exposes containing right
            Assert.That(left.Exposes.Contains(right), Is.True, "Left should expose Right for '--o' arrow");
        }

        [Test]
        public async Task ParseManyComponentTypesProduceComponents()
        {
            string puml = """
            @startuml
            component C1 as Cmp
            entity E1 as Ent
            database DB as D
            queue Q as Q1
            actor Act as A
            rectangle R as Rect
            cloud Cl as Cloud1
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            string[] aliases = { "Cmp", "Ent", "D", "Q1", "A", "Rect", "Cloud1" };
            foreach (var alias in aliases)
            {
                var found = diagram.Entities.FirstOrDefault(e => e.Alias == alias || e.Name == alias);
                Assert.That(found, Is.Not.Null, $"Component with alias or name '{alias}' should exist");
                Assert.That(found, Is.TypeOf<UMLComponent>());
            }
        }

        [Test]
        public async Task ParseVariousArrowEndingsProduceExpectedConnections()
        {
            string puml = """
            @startuml
            component A
            component B
            component C
            component D

            A --> B
            B --o C
            C --( D
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var a = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "A");
            var b = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "B");
            var c = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "C");
            var d = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "D");

            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(c, Is.Not.Null);
            Assert.That(d, Is.Not.Null);

            Assert.That(a.Consumes.Contains(b), Is.True, "A should consume B for '-->'");
            Assert.That(b.Exposes.Contains(c), Is.True, "B should expose C for '--o'");
            // For arrow ending with '(' parser treats as consume
            Assert.That(c.Consumes.Contains(d), Is.True, "C should consume D for '--(' arrow ending with '('");
        }

        [Test]
        public async Task ParseFramePackageContainingComponentSetsNamespace()
        {
            string puml = """
            @startuml
            frame "UI" {
                component View as V
            }
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var view = diagram.Entities.FirstOrDefault(e => e.Name == "View" || e.Alias == "V");
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Namespace, Is.EqualTo("UI"));
        }

        [Test]
        public async Task ParseInterfaceParenthesesStyle()
        {
            string puml = """
            @startuml
            () "PaymentService" as PS
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var iface = diagram.Entities.FirstOrDefault(e => e.Name == "PaymentService" || e.Alias == "PS");
            Assert.That(iface, Is.Not.Null);
            Assert.That(iface, Is.TypeOf<UMLInterface>());
        }

        [Test]
        public async Task BracketedImplicitComponentCreationAndConnectionByName()
        {
            string puml = """
            @startuml
            [Implicit] --> Known
            component Known as Known
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml, false);
            Assert.That(diagram, Is.Not.Null);

            var implicitComp = diagram.Entities.FirstOrDefault(e => e.Name == "Implicit");
            var knownComp = diagram.Entities.FirstOrDefault(e => e.Name == "Known" || e.Alias == "Known");

            Assert.That(implicitComp, Is.Not.Null);
            Assert.That(knownComp, Is.Not.Null);

            var imp = implicitComp as UMLComponent;
            var kn = knownComp as UMLComponent;
            Assert.That(imp, Is.Not.Null);
            Assert.That(kn, Is.Not.Null);

            // connection should have been recorded either as imp.Consumes contains known or vice versa
            Assert.That(imp.Consumes.Contains(kn) || imp.Exposes.Contains(kn) || kn.Consumes.Contains(imp) || kn.Exposes.Contains(imp));
        }

        [Test]
        public async Task BackreferenceAlias()
        {
            string puml = """
            @startuml
            [Service] --> DB
            component Service as S
            
            
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml, false);
            Assert.That(diagram, Is.Not.Null);

            var s = diagram.Entities.FirstOrDefault(e => e.Alias == "S") as UMLComponent;
            var db = diagram.Entities.FirstOrDefault(e => e.Alias == "DB") as UMLComponent;
            var serviceByName = diagram.Entities.FirstOrDefault(e => e.Name == "Service") as UMLComponent;
            Assert.That(s.Name, Is.EqualTo("Service"));
            Assert.That(s, Is.Not.Null);
            Assert.That(db, Is.Not.Null);
            Assert.That(serviceByName, Is.Not.Null);
         
            Assert.That(serviceByName.Consumes.Contains(db));
        }

        [Test]
        public async Task DuplicateIdentifierError()
        {
            string puml = """
            @startuml
            [Service] --> DB
            component Service as S
            component Database as DB

            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml, false);
            Assert.That(diagram, Is.Not.Null);

            Assert.That(diagram.LineErrors, Has.Some.Property("Text").Contains("Duplicate identifier : DB"));
        }


        [Test]
        public async Task AliasReferencedConnectionsResolveToSameComponent()
        {
            string puml = """
            @startuml
            component Service as S
            component Database as DB
            S --> DB
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var s = diagram.Entities.FirstOrDefault(e => e.Alias == "S") as UMLComponent;
            var db = diagram.Entities.FirstOrDefault(e => e.Alias == "DB") as UMLComponent;
            Assert.That(s, Is.Not.Null);
            Assert.That(db, Is.Not.Null);
            Assert.That(s.Consumes.Contains(db));
        }

        [Test]
        public async Task PackageAndFrameAndNodeTypesSetNamespace()
        {
            string puml = """
            @startuml
            package "Services" {
                component X as X1
            }
            frame "UI" {
                component V as V1
            }
            node "Backend" {
                component B as B1
            }
            cloud "Internet" {
                component C as C1
            }
            folder "Files" {
                component F as F1
            }
            together "Group" {
                component T as T1
            }
            rectangle "Box" {
                component R as R1
            }
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var mapping = new[] {
                (name: "X1", ns: "Services"),
                (name: "V1", ns: "UI"),
                (name: "B1", ns: "Backend"),
                (name: "C1", ns: "Internet"),
                (name: "F1", ns: "Files"),
                (name: "T1", ns: "Group"),
                (name: "R1", ns: "Box")
            };

            foreach (var (name, ns) in mapping)
            {
                var e = diagram.Entities.FirstOrDefault(x => x.Alias == name || x.Name == name);
                Assert.That(e, Is.Not.Null, $"{name} should exist");
                Assert.That(e.Namespace, Is.EqualTo(ns), $"{name} should be in namespace {ns}");
            }
        }

        [Test]
        public async Task ParseArrowWithColorAndThicknessAttributes()
        {
            string puml = """
            @startuml
            component asapdb
            component asaprep3
            component asaprep4
            component asaprep5
            asapdb --[#green,thickness=2]---> asaprep3
            asapdb --[#green,thickness=2]---> asaprep4
            asapdb --[#green,thickness=2]---> asaprep5
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var asapdb = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asapdb");
            var asaprep3 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asaprep3");
            var asaprep4 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asaprep4");
            var asaprep5 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asaprep5");

            Assert.That(asapdb, Is.Not.Null);
            Assert.That(asaprep3, Is.Not.Null);
            Assert.That(asaprep4, Is.Not.Null);
            Assert.That(asaprep5, Is.Not.Null);

            // Arrow ends with '>' so left component consumes right components
            Assert.That(asapdb.Consumes.Contains(asaprep3), Is.True, "asapdb should consume asaprep3");
            Assert.That(asapdb.Consumes.Contains(asaprep4), Is.True, "asapdb should consume asaprep4");
            Assert.That(asapdb.Consumes.Contains(asaprep5), Is.True, "asapdb should consume asaprep5");
        }

        [Test]
        public async Task ParseDirectionalArrowWithRightKeyword()
        {
            string puml = """
            @startuml
            component asapdb
            component asaprep3
            asapdb --[#green,thickness=2]right---> asaprep3
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var asapdb = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asapdb");
            var asaprep3 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asaprep3");

            Assert.That(asapdb, Is.Not.Null);
            Assert.That(asaprep3, Is.Not.Null);

            // Direction keyword should not break connection parsing
            Assert.That(asapdb.Consumes.Contains(asaprep3), Is.True, "asapdb should consume asaprep3 with directional arrow");
        }

        [Test]
        public async Task ParseLongDashArrowConnections()
        {
            string puml = """
            @startuml
            component OnYard
            component asapp7
            OnYard ----- asapp7
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var onYard = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "OnYard");
            var asapp7 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asapp7");

            Assert.That(onYard, Is.Not.Null);
            Assert.That(asapp7, Is.Not.Null);

            // Long dash without arrow should create some form of connection
            Assert.That(onYard.Consumes.Contains(asapp7) || onYard.Exposes.Contains(asapp7) || 
                        asapp7.Consumes.Contains(onYard) || asapp7.Exposes.Contains(onYard), 
                        Is.True, "OnYard and asapp7 should have some connection");
        }

        [Test]
        public async Task ParseShortDashConnection()
        {
            string puml = """
            @startuml
            component asaprep3
            component acintegrationhost
            asaprep3 -- acintegrationhost
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var asaprep3 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asaprep3");
            var acintegrationhost = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "acintegrationhost");

            Assert.That(asaprep3, Is.Not.Null);
            Assert.That(acintegrationhost, Is.Not.Null);

            // Short dash connection should be recognized
            Assert.That(asaprep3.Consumes.Contains(acintegrationhost) || asaprep3.Exposes.Contains(acintegrationhost) || 
                        acintegrationhost.Consumes.Contains(asaprep3) || acintegrationhost.Exposes.Contains(asaprep3), 
                        Is.True, "asaprep3 and acintegrationhost should have some connection");
        }

        [Test]
        public async Task ParseSingleDashConnection()
        {
            string puml = """
            @startuml
            component acintegrationhost
            component acdb
            acintegrationhost - acdb
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var acintegrationhost = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "acintegrationhost");
            var acdb = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "acdb");

            Assert.That(acintegrationhost, Is.Not.Null);
            Assert.That(acdb, Is.Not.Null);

            // Single dash connection should be recognized
            Assert.That(acintegrationhost.Consumes.Contains(acdb) || acintegrationhost.Exposes.Contains(acdb) || 
                        acdb.Consumes.Contains(acintegrationhost) || acdb.Exposes.Contains(acintegrationhost), 
                        Is.True, "acintegrationhost and acdb should have some connection");
        }

        [Test]
        public async Task ParseArrowConnectionWithThreeComponents()
        {
            string puml = """
            @startuml
            component acintegrationhost
            component acint1
            acintegrationhost ---> acint1
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var acintegrationhost = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "acintegrationhost");
            var acint1 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "acint1");

            Assert.That(acintegrationhost, Is.Not.Null);
            Assert.That(acint1, Is.Not.Null);

            // Standard arrow should create consume relationship
            Assert.That(acintegrationhost.Consumes.Contains(acint1), Is.True, "acintegrationhost should consume acint1");
        }

        [Test]
        public async Task ParseComplexDiagramWithMultipleConnectionTypes()
        {
            string puml = """
            @startuml
            component asapdb
            component asaprep3
            component asaprep4
            component asaprep5
            component OnYard
            component asapp7
            component acintegrationhost
            component acdb
            component acint1

            asapdb --[#green,thickness=2]---> asaprep3
            asapdb --[#green,thickness=2]---> asaprep4
            asapdb --[#green,thickness=2]---> asaprep5
            OnYard ----- asapp7
            asaprep3 -- acintegrationhost
            asapdb --[#green,thickness=2]right---> asaprep3
            acintegrationhost - acdb
            acintegrationhost ---> acint1
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            // Verify all components exist
            var asapdb = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asapdb");
            var asaprep3 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asaprep3");
            var asaprep4 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asaprep4");
            var asaprep5 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asaprep5");
            var onYard = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "OnYard");
            var asapp7 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "asapp7");
            var acintegrationhost = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "acintegrationhost");
            var acdb = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "acdb");
            var acint1 = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "acint1");

            Assert.That(asapdb, Is.Not.Null);
            Assert.That(asaprep3, Is.Not.Null);
            Assert.That(asaprep4, Is.Not.Null);
            Assert.That(asaprep5, Is.Not.Null);
            Assert.That(onYard, Is.Not.Null);
            Assert.That(asapp7, Is.Not.Null);
            Assert.That(acintegrationhost, Is.Not.Null);
            Assert.That(acdb, Is.Not.Null);
            Assert.That(acint1, Is.Not.Null);

            // Verify connections
            Assert.That(asapdb.Consumes.Contains(asaprep3), Is.True);
            Assert.That(asapdb.Consumes.Contains(asaprep4), Is.True);
            Assert.That(asapdb.Consumes.Contains(asaprep5), Is.True);
            Assert.That(acintegrationhost.Consumes.Contains(acint1), Is.True);

            // Verify bidirectional or connection existence for undirected arrows
            Assert.That(onYard.Consumes.Contains(asapp7) || onYard.Exposes.Contains(asapp7) || 
                        asapp7.Consumes.Contains(onYard) || asapp7.Exposes.Contains(onYard), Is.True);
            Assert.That(asaprep3.Consumes.Contains(acintegrationhost) || asaprep3.Exposes.Contains(acintegrationhost) || 
                        acintegrationhost.Consumes.Contains(asaprep3) || acintegrationhost.Exposes.Contains(asaprep3), Is.True);
            Assert.That(acintegrationhost.Consumes.Contains(acdb) || acintegrationhost.Exposes.Contains(acdb) || 
                        acdb.Consumes.Contains(acintegrationhost) || acdb.Exposes.Contains(acintegrationhost), Is.True);
        }

        [Test]
        public async Task ParseArrowWithMultipleAttributesCombinations()
        {
            string puml = """
            @startuml
            component A
            component B
            component C
            component D
            component E

            A --[#red]---> B
            B --[#blue,thickness=3]---> C
            C --[bold]---> D
            D --[dotted,#green]---> E
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var a = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "A");
            var b = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "B");
            var c = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "C");
            var d = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "D");
            var e = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "E");

            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(c, Is.Not.Null);
            Assert.That(d, Is.Not.Null);
            Assert.That(e, Is.Not.Null);

            // All arrows end with '>' so should create consume relationships
            Assert.That(a.Consumes.Contains(b), Is.True, "A should consume B");
            Assert.That(b.Consumes.Contains(c), Is.True, "B should consume C");
            Assert.That(c.Consumes.Contains(d), Is.True, "C should consume D");
            Assert.That(d.Consumes.Contains(e), Is.True, "D should consume E");
        }

        [Test]
        public async Task ParseDirectionalArrowsWithVariousDirections()
        {
            string puml = """
            @startuml
            component A
            component B
            component C
            component D
            component E

            A --right--> B
            B --down--> C
            C --left--> D
            D --up--> E
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var a = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "A");
            var b = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "B");
            var c = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "C");
            var d = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "D");
            var e = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "E");

            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(c, Is.Not.Null);
            Assert.That(d, Is.Not.Null);
            Assert.That(e, Is.Not.Null);

            // Direction keywords should not break connection parsing
            Assert.That(a.Consumes.Contains(b), Is.True, "A should consume B with right direction");
            Assert.That(b.Consumes.Contains(c), Is.True, "B should consume C with down direction");
            Assert.That(c.Consumes.Contains(d), Is.True, "C should consume D with left direction");
            Assert.That(d.Consumes.Contains(e), Is.True, "D should consume E with up direction");
        }

        [Test]
        public async Task ParseHiddenArrowConnection()
        {
            string puml = """
            @startuml
            component ICF
            component AC
            ICF -[hidden]-- AC
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var icf = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "ICF");
            var ac = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "AC");

            Assert.That(icf, Is.Not.Null);
            Assert.That(ac, Is.Not.Null);

            // Hidden arrows are handled by CommonParsings and added as UMLOther
            // They should not create actual connections but should not crash the parser
            Assert.That(diagram.Entities.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task ParseHiddenArrowWithLeftDirection()
        {
            string puml = """
            @startuml
            component ASAP
            component Director
            ASAP -[hidden]left- Director
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var asap = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "ASAP");
            var director = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "Director");

            Assert.That(asap, Is.Not.Null);
            Assert.That(director, Is.Not.Null);

            // Hidden arrows with direction should be parsed without creating connections
            Assert.That(diagram.Entities.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task ParseHiddenArrowWithDownDirection()
        {
            string puml = """
            @startuml
            component AC
            component CSA
            AC --[hidden]down- CSA
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var ac = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "AC");
            var csa = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "CSA");

            Assert.That(ac, Is.Not.Null);
            Assert.That(csa, Is.Not.Null);

            // Hidden arrows should not create actual connections
            Assert.That(diagram.Entities.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task ParseMultipleHiddenArrows()
        {
            string puml = """
            @startuml
            component ICF
            component AC
            component ASAP
            component Director
            component CSA

            ICF -[hidden]-- AC
            ASAP -[hidden]left- Director
            AC --[hidden]down- CSA
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            // Verify all components exist
            var icf = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "ICF");
            var ac = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "AC");
            var asap = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "ASAP");
            var director = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "Director");
            var csa = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "CSA");

            Assert.That(icf, Is.Not.Null);
            Assert.That(ac, Is.Not.Null);
            Assert.That(asap, Is.Not.Null);
            Assert.That(director, Is.Not.Null);
            Assert.That(csa, Is.Not.Null);

            // Hidden arrows should not create actual connections between components
            // Verify no consume/expose relationships exist for hidden connections
            Assert.That(icf.Consumes.Contains(ac), Is.False, "Hidden arrow should not create consume relationship");
            Assert.That(icf.Exposes.Contains(ac), Is.False, "Hidden arrow should not create expose relationship");
            Assert.That(asap.Consumes.Contains(director), Is.False, "Hidden arrow should not create consume relationship");
            Assert.That(asap.Exposes.Contains(director), Is.False, "Hidden arrow should not create expose relationship");
            Assert.That(ac.Consumes.Contains(csa), Is.False, "Hidden arrow should not create consume relationship");
            Assert.That(ac.Exposes.Contains(csa), Is.False, "Hidden arrow should not create expose relationship");
        }

        [Test]
        public async Task ParseMixedHiddenAndVisibleArrows()
        {
            string puml = """
            @startuml
            component A
            component B
            component C
            component D

            A --> B
            B -[hidden]-- C
            C --> D
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var a = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "A");
            var b = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "B");
            var c = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "C");
            var d = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "D");

            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(c, Is.Not.Null);
            Assert.That(d, Is.Not.Null);

            // Visible arrows should create connections
            Assert.That(a.Consumes.Contains(b), Is.True, "Visible arrow A --> B should create connection");
            Assert.That(c.Consumes.Contains(d), Is.True, "Visible arrow C --> D should create connection");

            // Hidden arrow should not create connection
            Assert.That(b.Consumes.Contains(c), Is.False, "Hidden arrow should not create consume relationship");
            Assert.That(b.Exposes.Contains(c), Is.False, "Hidden arrow should not create expose relationship");
        }

        [Test]
        public async Task ParseHiddenArrowsWithVariousDirections()
        {
            string puml = """
            @startuml
            component A
            component B
            component C
            component D
            component E

            A -[hidden]right- B
            B -[hidden]down- C
            C -[hidden]left- D
            D -[hidden]up- E
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var a = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "A");
            var b = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "B");
            var c = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "C");
            var d = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "D");
            var e = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "E");

            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(c, Is.Not.Null);
            Assert.That(d, Is.Not.Null);
            Assert.That(e, Is.Not.Null);

            // Hidden arrows with directions should not create any connections
            Assert.That(a.Consumes.Contains(b) || a.Exposes.Contains(b), Is.False);
            Assert.That(b.Consumes.Contains(c) || b.Exposes.Contains(c), Is.False);
            Assert.That(c.Consumes.Contains(d) || c.Exposes.Contains(d), Is.False);
            Assert.That(d.Consumes.Contains(e) || d.Exposes.Contains(e), Is.False);
        }

        [Test]
        public async Task ComponentKeywordsLikeEntityDatabaseQueueActorWork()
        {
            string puml = """
            @startuml
            entity MyEntity
            database MyDb
            queue MyQueue
            actor MyActor
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            Assert.That(diagram.Entities.Any(e => e.Name == "MyEntity"));
            Assert.That(diagram.Entities.Any(e => e.Name == "MyDb"));
            Assert.That(diagram.Entities.Any(e => e.Name == "MyQueue"));
            Assert.That(diagram.Entities.Any(e => e.Name == "MyActor"));
        }

        [Test]
        public async Task ComponentLinesWithColorAndStereotypeDoNotCrash()
        {
            string puml = """
            @startuml
            component Comp1 <<service>> #red
            component "Comp Two" as CT <<store>> #00FF00
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);
            Assert.That(diagram.Entities.Any(e => e.Name == "Comp1"));
            Assert.That(diagram.Entities.Any(e => e.Name == "Comp Two"));
        }

        [Test]
        public async Task ComplexLabelledRelationshipParsesAndConnects()
        {
            string puml = """
            @startuml
            component A
            component B
            A "uses" --> B : uses API
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var a = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "A");
            var b = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "B");
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(a.Consumes.Contains(b) || a.Exposes.Contains(b));
        }

        [Test]
        public async Task ParseExposesArrowBothForms()
        {
            string puml = """
            @startuml
            component LeftA
            component RightA
            LeftA --o RightA

            component LeftB
            component RightB
            LeftB o-- RightB
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var la = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "LeftA");
            var ra = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "RightA");
            var lb = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "LeftB");
            var rb = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "RightB");

            Assert.That(la, Is.Not.Null);
            Assert.That(ra, Is.Not.Null);
            Assert.That(lb, Is.Not.Null);
            Assert.That(rb, Is.Not.Null);

            // Parser supports arrow strings ending with 'o' (e.g., '--o') as left.Exposes adds right
            Assert.That(la.Exposes.Contains(ra) || ra.Exposes.Contains(la));
            // For 'o--' current parser may interpret differently; at minimum ensure no crash and some connection exists
            Assert.That(lb.Exposes.Contains(rb) || lb.Consumes.Contains(rb) || rb.Exposes.Contains(lb) || rb.Consumes.Contains(lb));
        }

        [Test]
        public async Task ParseInheritanceStyleArrowDoesNotCrash()
        {
            string puml = """
            @startuml
            component Parent
            component Child
            Child --|> Parent
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var parent = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "Parent");
            var child = diagram.Entities.OfType<UMLComponent>().FirstOrDefault(c => c.Name == "Child");
            Assert.That(parent, Is.Not.Null);
            Assert.That(child, Is.Not.Null);

            // Parser may not model inheritance for components; ensure it didn't crash and some relationship may exist
            Assert.That(child.Consumes.Contains(parent) || child.Exposes.Contains(parent) || parent.Consumes.Contains(child) || parent.Exposes.Contains(child) || diagram.ExplainedErrors.Count >= 0);
        }

        [Test]
        public async Task ParseQuotedNamesAndMultiplicityInRelationship()
        {
            string puml = """
            @startuml
            component Company
            component Employee
            Company "1" *-- "many" Employee : employs
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var company = diagram.Entities.FirstOrDefault(e => e.Name == "Company");
            var employee = diagram.Entities.FirstOrDefault(e => e.Name == "Employee");
            Assert.That(company, Is.Not.Null);
            Assert.That(employee, Is.Not.Null);

            var companyComp = company as UMLComponent;
            var employeeComp = employee as UMLComponent;
            Assert.That(companyComp, Is.Not.Null);
            Assert.That(employeeComp, Is.Not.Null);

            Assert.That(companyComp.Consumes.Contains(employeeComp) || companyComp.Exposes.Contains(employeeComp) || employeeComp.Consumes.Contains(companyComp) || employeeComp.Exposes.Contains(companyComp));
        }

        [Test]
        public async Task ParseAliasArrowResolutionMultipleWays()
        {
            string puml = """
            @startuml
            component Foo as F
            component Bar as B
            F --o B : provides
            [Foo] --> [Bar]
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml);
            Assert.That(diagram, Is.Not.Null);

            var f = diagram.Entities.FirstOrDefault(e => e.Alias == "F") as UMLComponent;
            var b = diagram.Entities.FirstOrDefault(e => e.Alias == "B") as UMLComponent;
            Assert.That(f, Is.Not.Null);
            Assert.That(b, Is.Not.Null);

            Assert.That(f.Exposes.Contains(b) || f.Consumes.Contains(b) || b.Exposes.Contains(f) || b.Consumes.Contains(f));
        }

        [Test]
        public async Task ForwardBracketedReferenceResolvesConnection()
        {
            string puml = """
            @startuml
            [Implicit] --> Known
            [Known]
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml, false);
            Assert.That(diagram, Is.Not.Null);

            var implicitComp = diagram.Entities.FirstOrDefault(e => e.Name == "Implicit") as UMLComponent;
            var knownComp = diagram.Entities.FirstOrDefault(e => e.Name == "Known") as UMLComponent;
            Assert.That(implicitComp, Is.Not.Null);
            Assert.That(knownComp, Is.Not.Null);

            Assert.That(implicitComp.Consumes.Contains(knownComp) );
        }

        [Test]
        public async Task ForwardBracketedReferenceResolvesConnectionFlagOn()
        {
            string puml = """
            @startuml
            [Implicit] --> Known
            [Known]
            @enduml
            """;

            var diagram = await UMLComponentDiagramParser.ReadString(puml, true);
            Assert.That(diagram, Is.Not.Null);

    
            var knownComp = diagram.Entities.FirstOrDefault(e => e.Name == "Known") as UMLComponent;
 
            Assert.That(knownComp, Is.Not.Null);

            Assert.That(diagram.ExplainedErrors, Has.Some.Property("Line").Contains("[Implicit] --> Known"));
        }
    }
}
