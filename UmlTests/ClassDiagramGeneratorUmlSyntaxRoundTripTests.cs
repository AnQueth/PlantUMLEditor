using NUnit.Framework;
using PlantUML;
using System.IO;
using UMLModels;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace UmlTests
{
    [TestFixture]
    public class ClassDiagramGeneratorUmlSyntaxRoundTripTests
    {
        private string GeneratePlantUML(UMLClassDiagram diagram)
        {
            using (var writer = new StringWriter())
            {
                ClassDiagramGeneratorUmlSyntax.Create(diagram, writer);
                return writer.ToString();
            }
        }

        private async Task<UMLClassDiagram?> ParsePlantUmlAsync(string plant)
        {
            return await UMLClassDiagramParser.ReadString(plant);
        }

        private UMLClassDiagram CreateSimpleDiagram(string title = "RoundTrip")
        {
            return new UMLClassDiagram(title, "roundtrip.puml");
        }

        [Test]
        public async Task RoundTrip_PropertyAndMethod_Preserved()
        {
            var diagram = CreateSimpleDiagram();
            var user = new UMLClass("", "", null, false, "User", Enumerable.Empty<UMLDataType>(), new UMLInterface[0]);
            user.Properties.Add(new UMLProperty("name", new StringDataType(), UMLVisibility.Public, ListTypes.None, false, false, false, null));
            user.Methods.Add(new UMLMethod("getName", new StringDataType(), UMLVisibility.Public));

            diagram.Package.Children.Add(user);

            var plant = GeneratePlantUML(diagram);

            var parsed = await ParsePlantUmlAsync(plant);
            Assert.That(parsed, Is.Not.Null);

            var parsedUser = parsed!.DataTypes.FirstOrDefault(d => d.Name == "User");
            Assert.That(parsedUser, Is.Not.Null, "Parsed diagram should contain User class");

            var parsedProp = parsedUser!.Properties.FirstOrDefault(p => p.Name == "name");
            Assert.That(parsedProp, Is.Not.Null, "Property 'name' should be present");
            Assert.That(parsedProp!.ObjectType.Name, Is.EqualTo("string"));
            Assert.That(parsedProp.Visibility, Is.EqualTo(UMLVisibility.Public));

            var parsedMethod = parsedUser.Methods.FirstOrDefault(m => m.Name == "getName");
            Assert.That(parsedMethod, Is.Not.Null, "Method 'getName' should be present");
            Assert.That(parsedMethod!.ReturnType.Name, Is.EqualTo("string"));
            Assert.That(parsedMethod.Visibility, Is.EqualTo(UMLVisibility.Public));
        }

        [Test]
        public async Task RoundTrip_StaticProperty_Preserved()
        {
            var diagram = CreateSimpleDiagram();
            var c = new UMLClass("", "", null, false, "Util", Enumerable.Empty<UMLDataType>(), new UMLInterface[0]);
            c.Properties.Add(new UMLProperty("Count", new IntDataType(), UMLVisibility.Public, ListTypes.None, true, false, false, "0"));
            diagram.Package.Children.Add(c);

            var plant = GeneratePlantUML(diagram);
            var parsed = await ParsePlantPumlAsync(plant);
            Assert.That(parsed, Is.Not.Null);

            var util = parsed!.DataTypes.FirstOrDefault(d => d.Name == "Util");
            Assert.That(util, Is.Not.Null);
            var prop = util!.Properties.FirstOrDefault(p => p.Name == "Count");
            Assert.That(prop, Is.Not.Null);
            Assert.That(prop!.IsStatic, Is.True, "Parsed property should be static");
            Assert.That(prop.DefaultValue?.Trim(), Is.EqualTo("= 0"));
        }

        [Test]
        public async Task RoundTrip_MethodWithParameters_Preserved()
        {
            var diagram = CreateSimpleDiagram();
            var svc = new UMLClass("", "", null, false, "Service", Enumerable.Empty<UMLDataType>(), new UMLInterface[0]);
            var p1 = new UMLParameter("id", new IntDataType(), ListTypes.None);
            var p2 = new UMLParameter("name", new StringDataType(), ListTypes.None);
            var method = new UMLMethod("Save", new VoidDataType(), UMLVisibility.Public, p1, p2);
            svc.Methods.Add(method);
            diagram.Package.Children.Add(svc);

            var plant = GeneratePlantUML(diagram);
            var parsed = await ParsePlantPumlAsync(plant);
            Assert.That(parsed, Is.Not.Null);

            var parsedSvc = parsed!.DataTypes.FirstOrDefault(d => d.Name == "Service");
            Assert.That(parsedSvc, Is.Not.Null);
            var parsedMethod = parsedSvc!.Methods.FirstOrDefault(m => m.Name == "Save");
            Assert.That(parsedMethod, Is.Not.Null);
            Assert.That(parsedMethod!.Parameters.Count, Is.EqualTo(2));
            Assert.That(parsedMethod.Parameters[0].Name, Is.EqualTo("id"));
            Assert.That(parsedMethod.Parameters[0].ObjectType.Name, Is.EqualTo("int"));
            Assert.That(parsedMethod.Parameters[1].Name, Is.EqualTo("name"));
            Assert.That(parsedMethod.Parameters[1].ObjectType.Name, Is.EqualTo("string"));
        }

        [Test]
        public async Task RoundTrip_InheritanceAndInterface_Preserved()
        {
            var diagram = CreateSimpleDiagram();
            var animal = new UMLClass("", "", null, false, "Animal", Enumerable.Empty<UMLDataType>(), new UMLInterface[0]);
            var petIface = new UMLInterface("", "IPet", null, new UMLInterface[0]);
            var dog = new UMLClass("", "", null, false, "Dog", new UMLDataType[] { animal }, petIface);

            diagram.Package.Children.Add(animal);
            diagram.Package.Children.Add(petIface);
            diagram.Package.Children.Add(dog);

            var plant = GeneratePlantUML(diagram);
            var parsed = await ParsePlantUmlAsync(plant);
            Assert.That(parsed, Is.Not.Null);

            var parsedDog = parsed!.DataTypes.FirstOrDefault(d => d.Name == "Dog");
            Assert.That(parsedDog, Is.Not.Null);

            // Derived should reference base
            Assert.That(parsedDog!.Bases.Any(b => b.Name == "Animal"), Is.True, "Dog should have Animal as base");

            // Derived should reference interface
            Assert.That(parsedDog.Interfaces.Any(i => i.Name == "IPet"), Is.True, "Dog should implement IPet");
        }

        // helper renamed to avoid accidental typos
        private static Task<UMLClassDiagram?> ParsePlantPumlAsync(string plant) => UMLClassDiagramParser.ReadString(plant);
    }
}
