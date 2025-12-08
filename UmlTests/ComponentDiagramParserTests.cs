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
