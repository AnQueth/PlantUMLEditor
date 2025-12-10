using NUnit.Framework;
using PlantUML;
using System.IO;
using UMLModels;
using System.Linq;
using System.Collections.Generic;

namespace UmlTests
{
    [TestFixture]
    public class ClassDiagramGeneratorUmlSyntaxTests
    {
        private string GeneratePlantUML(UMLClassDiagram diagram)
        {
            using (var writer = new StringWriter())
            {
                ClassDiagramGeneratorUmlSyntax.Create(diagram, writer);
                return writer.ToString();
            }
        }

        private UMLClassDiagram CreateSimpleDiagram(string title = "Test Diagram")
        {
            return new UMLClassDiagram(title, "test.puml");
        }

        [Test]
        public void Generate_Property_UmlSyntax_NameColonType()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", Enumerable.Empty<UMLDataType>(), new UMLInterface[0]);
            var prop = new UMLProperty("name", new StringDataType(), UMLVisibility.Public, ListTypes.None, false, false, false, null);
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("+ name: string"));
        }

        [Test]
        public void Generate_Property_WithModifiers_IncludesStaticAndAbstract()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", Enumerable.Empty<UMLDataType>(), new UMLInterface[0]);
            var prop = new UMLProperty("count", new IntDataType(), UMLVisibility.Public, ListTypes.None, true, true, false, "0");
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("+ count: int = 0 {static,abstract}"));
        }

        [Test]
        public void Generate_Method_UmlSyntax_ReturnTypeAfterColon()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", Enumerable.Empty<UMLDataType>(), new UMLInterface[0]);
            var method = new UMLMethod("getName", new StringDataType(), UMLVisibility.Public);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls); 

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("+ getName(): string"));
        }

        [Test]
        public void Generate_Method_WithParameters_FormatsNameColonTypePairs()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", Enumerable.Empty<UMLDataType>(), new UMLInterface[0]);
            var p1 = new UMLParameter("name", new StringDataType(), ListTypes.None);
            var p2 = new UMLParameter("age", new IntDataType(), ListTypes.None);
            var method = new UMLMethod("setData", new VoidDataType(), UMLVisibility.Public, p1, p2);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("+ setData(name: string, age: int): void"));
        }

        [Test]
        public void Generate_CollectionProperty_IncludesMultiplicity()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "Company", Enumerable.Empty<UMLDataType>(), new UMLInterface[0]);
            var prop = new UMLProperty("employees", new StringDataType(), UMLVisibility.Public, ListTypes.List, false, false, false, null);
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("+ employees: string[*]"));
        }
    }
}
