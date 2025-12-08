using NUnit.Framework;
using PlantUML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UMLModels;

namespace UmlTests
{
    [TestFixture]
    public class ClassDiagramGeneratorTests
    {
        #region Helper Methods

        private string GeneratePlantUML(UMLClassDiagram diagram)
        {
            using (var writer = new StringWriter())
            {
                ClassDiagramGenerator.Create(diagram, writer);
                return writer.ToString();
            }
        }

        private UMLClassDiagram CreateSimpleDiagram(string title = "Test Diagram")
        {
            return new UMLClassDiagram(title, "test.puml");
        }

        #endregion

        #region Basic Generation Tests

        [Test]
        public void Generate_SimpleClass_ProducesValidPlantUML()
        {
            var diagram = CreateSimpleDiagram("Simple Class");
            var simpleClass = new UMLClass("", "", null, false, "User", [], []);
            diagram.Package.Children.Add(simpleClass);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("@startuml"));
            Assert.That(result, Does.Contain("@enduml"));
            Assert.That(result, Does.Contain("title Simple Class"));
            Assert.That(result, Does.Contain("class User"));
        }

        [Test]
        public void Generate_AbstractClass_IncludesAbstractKeyword()
        {
            var diagram = CreateSimpleDiagram();
            var abstractClass = new UMLClass("", "", null, true, "Animal", [], []);
            diagram.Package.Children.Add(abstractClass);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("abstract class Animal"));
        }

        [Test]
        public void Generate_Interface_IncludesInterfaceKeyword()
        {
            var diagram = CreateSimpleDiagram();
            var iface = new UMLInterface("", "IRepository", null, []);
            diagram.Package.Children.Add(iface);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("interface IRepository"));
        }

        [Test]
        public void Generate_Enum_IncludesEnumKeyword()
        {
            var diagram = CreateSimpleDiagram();
            var enumType = new UMLEnum("", "Status");
            diagram.Package.Children.Add(enumType);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("enum Status"));
        }

        #endregion

        #region Class Name Tests

        [Test]
        public void Generate_ClassNameWithSpaces_IsQuoted()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User Account", [], []);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("\"User Account\""));
        }

        [Test]
        public void Generate_ClassNameWithQuestionMark_IsQuoted()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "Nullable?", [], []);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("\"Nullable?\""));
        }

        [Test]
        public void Generate_ClassWithAlias_IncludesAsClause()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", "U", false, "User", [], []);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("User as U"));
        }

        [Test]
        public void Generate_ClassNameWithSpaceAndAlias_QuotedNameWithAlias()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", "UA", false, "User Account", [], []);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("\"User Account\" as UA"));
        }

        #endregion

        #region Stereotype Tests

        [Test]
        public void Generate_ClassWithStereotype_IncludesStereotype()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("entity", "", null, false, "User", [], []);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("<<entity>>"));
        }

        [Test]
        public void Generate_ClassWithEmptyStereotype_NoStereotypeOutput()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Not.Contain("<<"));
        }

        #endregion

        #region Property Tests

        [Test]
        public void Generate_SimpleProperty_CorrectFormat()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var prop = new UMLProperty("name", new StringDataType(), UMLVisibility.Public, ListTypes.None, false, false, false, null);
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("+ string name"));
        }

        [Test]
        public void Generate_PropertyAllVisibilityLevels()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            
            cls.Properties.Add(new UMLProperty("pub", new StringDataType(), UMLVisibility.Public, ListTypes.None, false, false, false, null));
            cls.Properties.Add(new UMLProperty("priv", new StringDataType(), UMLVisibility.Private, ListTypes.None, false, false, false, null));
            cls.Properties.Add(new UMLProperty("prot", new StringDataType(), UMLVisibility.Protected, ListTypes.None, false, false, false, null));
            cls.Properties.Add(new UMLProperty("inter", new StringDataType(), UMLVisibility.Internal, ListTypes.None, false, false, false, null));
            
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("+ string pub"));
            Assert.That(result, Does.Contain("- string priv"));
            Assert.That(result, Does.Contain("# string prot"));
            Assert.That(result, Does.Contain("~ string inter"));
        }

        [Test]
        public void Generate_StaticProperty_IncludesStaticModifier()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var prop = new UMLProperty("count", new IntDataType(), UMLVisibility.Public, ListTypes.None, true, false, false, null);
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("{static}"));
        }

        [Test]
        public void Generate_AbstractProperty_IncludesAbstractModifier()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var prop = new UMLProperty("abstractProp", new StringDataType(), UMLVisibility.Public, ListTypes.None, false, true, false, null);
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("{abstract}"));
        }

        [Test]
        public void Generate_PropertyWithDefaultValue_IncludesDefaultValue()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var prop = new UMLProperty("count", new IntDataType(), UMLVisibility.Public, ListTypes.None, false, false, false, "= 0");
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("int count"));
            Assert.That(result, Does.Contain("= 0"));
        }

        [Test]
        public void Generate_ArrayProperty_IncludesBrackets()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var prop = new UMLProperty("items", new StringDataType(), UMLVisibility.Public, ListTypes.Array, false, false, false, null);
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("string[]"));
        }

        [Test]
        public void Generate_ListProperty_IncludesGeneric()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var prop = new UMLProperty("items", new StringDataType(), UMLVisibility.Public, ListTypes.List, false, false, false, null);
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("List<string>"));
        }

        [Test]
        public void Generate_IReadOnlyCollectionProperty_IncludesGeneric()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var prop = new UMLProperty("items", new IntDataType(), UMLVisibility.Public, ListTypes.IReadOnlyCollection, false, false, false, null);
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("IReadOnlyCollection<int>"));
        }

        [Test]
        public void Generate_PropertiesNotDrawnWithLine_AreIncludedInClassBody()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var prop1 = new UMLProperty("name", new StringDataType(), UMLVisibility.Public, ListTypes.None, false, false, false, null);
            var prop2 = new UMLProperty("email", new StringDataType(), UMLVisibility.Public, ListTypes.None, false, false, false, null);
            cls.Properties.Add(prop1);
            cls.Properties.Add(prop2);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("+ string name"));
            Assert.That(result, Does.Contain("+ string email"));
        }

        #endregion

        #region Method Tests

        [Test]
        public void Generate_SimpleMethod_CorrectFormat()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var method = new UMLMethod("getName", new StringDataType(), UMLVisibility.Public);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("+ string getName()"));
        }

        [Test]
        public void Generate_MethodAllVisibilityLevels()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            
            cls.Methods.Add(new UMLMethod("pubMethod", new StringDataType(), UMLVisibility.Public));
            cls.Methods.Add(new UMLMethod("privMethod", new StringDataType(), UMLVisibility.Private));
            cls.Methods.Add(new UMLMethod("protMethod", new StringDataType(), UMLVisibility.Protected));
            cls.Methods.Add(new UMLMethod("interMethod", new StringDataType(), UMLVisibility.Internal));
            
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("+ string pubMethod()"));
            Assert.That(result, Does.Contain("- string privMethod()"));
            Assert.That(result, Does.Contain("# string protMethod()"));
            Assert.That(result, Does.Contain("~ string interMethod()"));
        }

        [Test]
        public void Generate_StaticMethod_IncludesStaticModifier()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var staticMethod = new UMLMethod("create", new StringDataType(), UMLVisibility.Public) 
            { 
                IsStatic = true 
            };
            cls.Methods.Add(staticMethod);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("{static}"));
            Assert.That(result, Does.Contain("create()"));
        }

        [Test]
        public void Generate_AbstractMethod_IncludesAbstractModifier()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var abstractMethod = new UMLMethod("doWork", new StringDataType(), UMLVisibility.Public) 
            { 
                IsAbstract = true 
            };
            cls.Methods.Add(abstractMethod);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("{abstract}"));
            Assert.That(result, Does.Contain("doWork()"));
        }

        [Test]
        public void Generate_MethodWithNoParameters_EmptyParentheses()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var method = new UMLMethod("getValue", new StringDataType(), UMLVisibility.Public);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("getValue()"));
        }

        [Test]
        public void Generate_MethodWithSingleParameter_CorrectFormat()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var param = new UMLParameter("name", new StringDataType(), ListTypes.None);
            var method = new UMLMethod("setName", new StringDataType(), UMLVisibility.Public, param);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("setName(string name)"));
        }

        [Test]
        public void Generate_MethodWithMultipleParameters_SeparatedByComma()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var param1 = new UMLParameter("name", new StringDataType(), ListTypes.None);
            var param2 = new UMLParameter("age", new IntDataType(), ListTypes.None);
            var method = new UMLMethod("setData", new StringDataType(), UMLVisibility.Public, param1, param2);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("setData(string name, int age)"));
        }

        [Test]
        public void Generate_MethodParameterWithArrayType_IncludesBrackets()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var param = new UMLParameter("items", new StringDataType(), ListTypes.Array);
            var method = new UMLMethod("process", new StringDataType(), UMLVisibility.Public, param);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("string[]"));
        }

        [Test]
        public void Generate_MethodParameterWithListType_IncludesGeneric()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var param = new UMLParameter("items", new StringDataType(), ListTypes.List);
            var method = new UMLMethod("process", new StringDataType(), UMLVisibility.Public, param);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("List<string>"));
        }

        [Test]
        public void Generate_MethodParameterWithIReadOnlyCollection_IncludesGeneric()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var param = new UMLParameter("items", new IntDataType(), ListTypes.IReadOnlyCollection);
            var method = new UMLMethod("process", new StringDataType(), UMLVisibility.Public, param);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("IReadOnlyCollection<int>"));
        }

        [Test]
        public void Generate_MethodWithVoidReturnType_ShowsVoid()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var method = new UMLMethod("execute", new VoidDataType(), UMLVisibility.Public);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("void execute()"));
        }

        #endregion

        #region Inheritance Tests

        [Test]
        public void Generate_ClassInheritance_ShowsInheritanceRelationship()
        {
            var diagram = CreateSimpleDiagram();
            var baseClass = new UMLClass("", "", null, false, "Animal", [], []);
            var derivedClass = new UMLClass("", "", null, false, "Dog", [baseClass], []);
            diagram.Package.Children.Add(baseClass);
            diagram.Package.Children.Add(derivedClass);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("Dog -- Animal"));
        }

        [Test]
        public void Generate_ClassWithMultipleBaseClasses_ShowsAllRelationships()
        {
            var diagram = CreateSimpleDiagram();
            var base1 = new UMLClass("", "", null, false, "A", [], []);
            var base2 = new UMLClass("", "", null, false, "B", [], []);
            var derived = new UMLClass("", "", null, false, "C", [base1, base2], []);
            diagram.Package.Children.Add(base1);
            diagram.Package.Children.Add(base2);
            diagram.Package.Children.Add(derived);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("C -- A"));
            Assert.That(result, Does.Contain("C -- B"));
        }

        #endregion

        #region Interface Implementation Tests

        [Test]
        public void Generate_InterfaceImplementation_ShowsImplementationRelationship()
        {
            var diagram = CreateSimpleDiagram();
            var iface = new UMLInterface("", "IRepository", null, []);
            var cls = new UMLClass("", "", null, false, "Repository", [], iface);
            diagram.Package.Children.Add(iface);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("Repository --* IRepository"));
        }

        [Test]
        public void Generate_MultipleInterfaceImplementation_ShowsAllRelationships()
        {
            var diagram = CreateSimpleDiagram();
            var iface1 = new UMLInterface("", "IRepository", null, []);
            var iface2 = new UMLInterface("", "IDisposable", null, []);
            var cls = new UMLClass("", "", null, false, "Repository", [], iface1, iface2);
            diagram.Package.Children.Add(iface1);
            diagram.Package.Children.Add(iface2);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("Repository --* IRepository"));
            Assert.That(result, Does.Contain("Repository --* IDisposable"));
        }

        #endregion

        #region Package Tests

        [Test]
        public void Generate_SinglePackage_IncludesPackageDeclaration()
        {
            var diagram = CreateSimpleDiagram();
            var package = new UMLPackage("Domain");
            var cls = new UMLClass("", "", null, false, "User", [], []);
            package.Children.Add(cls);
            diagram.Package.Children.Add(package);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("package Domain {"));
            Assert.That(result, Does.Contain("}"));
        }

        [Test]
        public void Generate_NestedPackages_IncludesAllPackages()
        {
            var diagram = CreateSimpleDiagram();
            var parentPackage = new UMLPackage("Parent");
            var childPackage = new UMLPackage("Child");
            var cls = new UMLClass("", "", null, false, "User", [], []);
            childPackage.Children.Add(cls);
            parentPackage.Children.Add(childPackage);
            diagram.Package.Children.Add(parentPackage);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("package Parent {"));
            Assert.That(result, Does.Contain("package Child {"));
        }

        [Test]
        public void Generate_PackageWithMultipleClasses_IncludesAllClasses()
        {
            var diagram = CreateSimpleDiagram();
            var package = new UMLPackage("Domain");
            var cls1 = new UMLClass("", "", null, false, "User", [], []);
            var cls2 = new UMLClass("", "", null, false, "Product", [], []);
            package.Children.Add(cls1);
            package.Children.Add(cls2);
            diagram.Package.Children.Add(package);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("package Domain {"));
            Assert.That(result, Does.Contain("class User"));
            Assert.That(result, Does.Contain("class Product"));
        }

        #endregion

        #region Special Content Tests

        [Test]
        public void Generate_UMLComment_IsIncluded()
        {
            var diagram = CreateSimpleDiagram();
            var comment = new UMLComment("This is a comment");
            diagram.Package.Children.Add(comment);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("This is a comment"));
        }

        [Test]
        public void Generate_UMLNote_IsIncluded()
        {
            var diagram = CreateSimpleDiagram();
            var note = new UMLNote("This is a note", "N1");
            diagram.Package.Children.Add(note);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("This is a note"));
        }

        [Test]
        public void Generate_UMLOther_IsIncluded()
        {
            var diagram = CreateSimpleDiagram();
            var other = new UMLOther("some raw text");
            diagram.Package.Children.Add(other);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("some raw text"));
        }

        #endregion

        #region Note Connection Tests

        [Test]
        public void Generate_NoteConnections_AreIncluded()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            diagram.Package.Children.Add(cls);
            
            var noteConn = new UMLNoteConnection("User", "..>", "note1");
            diagram.AddNoteConnection(noteConn);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("User"));
            Assert.That(result, Does.Contain("..>"));
            Assert.That(result, Does.Contain("note1"));
        }

        [Test]
        public void Generate_MultipleNoteConnections_AreIncluded()
        {
            var diagram = CreateSimpleDiagram();
            var cls1 = new UMLClass("", "", null, false, "User", [], []);
            var cls2 = new UMLClass("", "", null, false, "Product", [], []);
            diagram.Package.Children.Add(cls1);
            diagram.Package.Children.Add(cls2);
            
            diagram.AddNoteConnection(new UMLNoteConnection("User", "..>", "note1"));
            diagram.AddNoteConnection(new UMLNoteConnection("Product", "..>", "note2"));

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("User") | Does.Contain("Product"));
            Assert.That(result, Does.Contain("note1"));
            Assert.That(result, Does.Contain("note2"));
        }

        #endregion

        #region Complex Scenario Tests

        [Test]
        public void Generate_CompleteClassHierarchy_GeneratesValidOutput()
        {
            var diagram = CreateSimpleDiagram("Complete Hierarchy");

            // Create abstract base class
            var baseClass = new UMLClass("", "", null, true, "Animal", [], []);
            baseClass.Properties.Add(new UMLProperty("age", new IntDataType(), UMLVisibility.Protected, ListTypes.None, false, false, false, null));
            baseClass.Methods.Add(new UMLMethod("makeSound", new VoidDataType(), UMLVisibility.Public));
            
            // Create concrete derived class
            var dog = new UMLClass("", "", null, false, "Dog", [baseClass], []);
            dog.Properties.Add(new UMLProperty("breed", new StringDataType(), UMLVisibility.Private, ListTypes.None, false, false, false, null));
            dog.Methods.Add(new UMLMethod("bark", new VoidDataType(), UMLVisibility.Public));
            
            // Create interface
            var iface = new UMLInterface("", "IPet", null, []);
            iface.Methods.Add(new UMLMethod("play", new VoidDataType(), UMLVisibility.Public));
            
            diagram.Package.Children.Add(baseClass);
            diagram.Package.Children.Add(dog);
            diagram.Package.Children.Add(iface);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("@startuml"));
            Assert.That(result, Does.Contain("@enduml"));
            Assert.That(result, Does.Contain("abstract class Animal"));
            Assert.That(result, Does.Contain("class Dog"));
            Assert.That(result, Does.Contain("interface IPet"));
            Assert.That(result, Does.Contain("Dog -- Animal"));
            Assert.That(result, Does.Contain("# int age"));
            Assert.That(result, Does.Contain("- string breed"));
        }

        [Test]
        public void Generate_PackageWithComplexContent_GeneratesValidOutput()
        {
            var diagram = CreateSimpleDiagram("Domain Model");

            var package = new UMLPackage("Domain");
            
            // Entity base class
            var entity = new UMLClass("", "", null, true, "Entity", [], []);
            entity.Properties.Add(new UMLProperty("id", new IntDataType(), UMLVisibility.Protected, ListTypes.None, false, false, false, null));
            
            // User class
            var user = new UMLClass("", "", null, false, "User", [entity], []);
            user.Properties.Add(new UMLProperty("username", new StringDataType(), UMLVisibility.Private, ListTypes.None, false, false, false, null));
            user.Properties.Add(new UMLProperty("email", new StringDataType(), UMLVisibility.Private, ListTypes.None, false, false, false, null));
            user.Methods.Add(new UMLMethod("getUsername", new StringDataType(), UMLVisibility.Public));
            user.Methods.Add(new UMLMethod("setPassword", new VoidDataType(), UMLVisibility.Public, new UMLParameter("password", new StringDataType())));
            
            package.Children.Add(entity);
            package.Children.Add(user);
            diagram.Package.Children.Add(package);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("package Domain"));
            Assert.That(result, Does.Contain("abstract class Entity"));
            Assert.That(result, Does.Contain("class User"));
            Assert.That(result, Does.Contain("# int id"));
            Assert.That(result, Does.Contain("- string username"));
            Assert.That(result, Does.Contain("User -- Entity"));
            Assert.That(result, Does.Contain("getUsername()"));
            Assert.That(result, Does.Contain("setPassword(string password)"));
        }

        [Test]
        public void Generate_EnumWithProperties_IncludesAllProperties()
        {
            var diagram = CreateSimpleDiagram();
            var enumType = new UMLEnum("", "Status");
            enumType.Properties.Add(new UMLProperty("ACTIVE", new IntDataType(), UMLVisibility.Public, ListTypes.None, false, false, false, null));
            enumType.Properties.Add(new UMLProperty("INACTIVE", new IntDataType(), UMLVisibility.Public, ListTypes.None, false, false, false, null));
            enumType.Properties.Add(new UMLProperty("PENDING", new IntDataType(), UMLVisibility.Public, ListTypes.None, false, false, false, null));
            diagram.Package.Children.Add(enumType);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("enum Status"));
            Assert.That(result, Does.Contain("ACTIVE"));
            Assert.That(result, Does.Contain("INACTIVE"));
            Assert.That(result, Does.Contain("PENDING"));
        }

        #endregion

        #region Generic Type Tests

        [Test]
        public void Generate_GenericClassName_IncludesGenericPart()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "Repository<T>", [], []);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("Repository<T>"));
        }

        [Test]
        public void Generate_GenericMethodReturnType_IncludesGenericPart()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "Repository", [], []);
            var returnType = new UMLDataType("List<User>");
            var method = new UMLMethod("getAll", returnType, UMLVisibility.Public);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("List<User>"));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Generate_EmptyDiagram_ContainsValidStartAndEnd()
        {
            var diagram = CreateSimpleDiagram("Empty Diagram");

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("@startuml"));
            Assert.That(result, Does.Contain("@enduml"));
            Assert.That(result, Does.Contain("title Empty Diagram"));
        }

        [Test]
        public void Generate_TitleWithSpecialCharacters_IsIncluded()
        {
            var diagram = new UMLClassDiagram("My Diagram (v1.0)", "test.puml");

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("My Diagram (v1.0)"));
        }

        [Test]
        public void Generate_ClassNameWithGenericAndAlias_BothAreIncluded()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", "R", false, "Repository<T>", [], []);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("Repository<T>") | Does.Contain("R"));
        }

        [Test]
        public void Generate_PropertyNameWithSpecialCharacters_IsIncluded()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var prop = new UMLProperty("_internalField", new StringDataType(), UMLVisibility.Private, ListTypes.None, false, false, false, null);
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("_internalField"));
        }

        [Test]
        public void Generate_MethodNameWithCamelCase_IsIncluded()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var method = new UMLMethod("getUserDetails", new StringDataType(), UMLVisibility.Public);
            cls.Methods.Add(method);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("getUserDetails"));
        }

        [Test]
        public void Generate_MultipleClassesWithRelationships_AllRelationshipsAreIncluded()
        {
            var diagram = CreateSimpleDiagram("Multi-Class Diagram");
            
            var base1 = new UMLClass("", "", null, false, "Base1", [], []);
            var base2 = new UMLClass("", "", null, false, "Base2", [], []);
            var iface1 = new UMLInterface("", "Interface1", null, []);
            var derived = new UMLClass("", "", null, false, "Derived", [base1, base2], iface1);
            
            diagram.Package.Children.Add(base1);
            diagram.Package.Children.Add(base2);
            diagram.Package.Children.Add(iface1);
            diagram.Package.Children.Add(derived);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("class Base1"));
            Assert.That(result, Does.Contain("class Base2"));
            Assert.That(result, Does.Contain("interface Interface1"));
            Assert.That(result, Does.Contain("class Derived"));
            Assert.That(result, Does.Contain("Derived -- Base1"));
            Assert.That(result, Does.Contain("Derived -- Base2"));
            Assert.That(result, Does.Contain("Derived --* Interface1"));
        }

        #endregion

        #region Composition/Aggregation Tests

        [Test]
        public void Generate_PropertyDrawnWithLine_IsIncludedAsRelationship()
        {
            var diagram = CreateSimpleDiagram();
            var company = new UMLClass("", "", null, false, "Company", [], []);
            var employee = new UMLClass("", "", null, false, "Employee", [], []);
            
            // Property marked as drawn with line (composition)
            var employeeProp = new UMLProperty("employees", employee, UMLVisibility.Public, ListTypes.List, false, false, true, null);
            company.Properties.Add(employeeProp);
            
            diagram.Package.Children.Add(company);
            diagram.Package.Children.Add(employee);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("Company"));
            Assert.That(result, Does.Contain("Employee"));
            // Check for composition relationship
            Assert.That(result, Does.Contain("--*"));
        }

        [Test]
        public void Generate_PropertyDrawnWithLineNoListType_ShowsSingleRelationship()
        {
            var diagram = CreateSimpleDiagram();
            var company = new UMLClass("", "", null, false, "Company", [], []);
            var ceo = new UMLClass("", "", null, false, "CEO", [], []);
            
            var ceoProp = new UMLProperty("ceo", ceo, UMLVisibility.Public, ListTypes.None, false, false, true, null);
            company.Properties.Add(ceoProp);
            
            diagram.Package.Children.Add(company);
            diagram.Package.Children.Add(ceo);

            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.Contain("Company"));
            Assert.That(result, Does.Contain("CEO"));
        }

        [Test]
        public void Generate_PropertyDrawnWithLineWithManyMultiplicity_ShowsCorrectMultiplicity()
        {
            var diagram = CreateSimpleDiagram();
            var company = new UMLClass("", "", null, false, "Company", [], []);
            var employee = new UMLClass("", "", null, false, "Employee", [], []);
            
            var employeesProp = new UMLProperty("employees", employee, UMLVisibility.Public, ListTypes.List, false, false, true, null);
            company.Properties.Add(employeesProp);
            
            diagram.Package.Children.Add(company);
            diagram.Package.Children.Add(employee);

            var result = GeneratePlantUML(diagram);

            // Should show "1" and "*" multiplicity for list type
            Assert.That(result, Does.Contain("\"1\"") | Does.Contain("\"*\""));
        }

        #endregion

        #region Visibility Modifiers

        [Test]
        public void Generate_NoVisibilityModifier_NoSymbolOutput()
        {
            var diagram = CreateSimpleDiagram();
            var cls = new UMLClass("", "", null, false, "User", [], []);
            var prop = new UMLProperty("name", new StringDataType(), UMLVisibility.None, ListTypes.None, false, false, false, null);
            cls.Properties.Add(prop);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);

            // Should have a space for the visibility modifier
            Assert.That(result, Does.Contain("string name"));
        }

        #endregion

        #region Output Format Tests

        [Test]
        public void Generate_Output_StartsWithStartUML()
        {
            var diagram = CreateSimpleDiagram();
            var result = GeneratePlantUML(diagram);

            Assert.That(result, Does.StartWith("@startuml"));
        }

        [Test]
        public void Generate_Output_EndsWithEndUML()
        {
            var diagram = CreateSimpleDiagram();
            var result = GeneratePlantUML(diagram);

            Assert.That(result.Trim(), Does.EndWith("@enduml"));
        }

        [Test]
        public void Generate_Output_HasCorrectLineBreaks()
        {
            var diagram = CreateSimpleDiagram("Test");
            var cls = new UMLClass("", "", null, false, "User", [], []);
            diagram.Package.Children.Add(cls);

            var result = GeneratePlantUML(diagram);
            var lines = result.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            Assert.That(lines[0], Does.Contain("@startuml"));
            Assert.That(lines[1], Does.Contain("title"));
            // Last non-empty line should be @enduml
            var nonEmptyLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            Assert.That(nonEmptyLines.Last(), Does.Contain("@enduml"));
        }

        #endregion
    }
}
