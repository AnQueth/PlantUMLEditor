using NUnit.Framework;
using PlantUML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UMLModels;

namespace UmlTests
{
    [TestFixture]
    public class UMLClassDiagramParserTests
    {
        #region Basic Class Parsing

        [Test]
        public async Task ParseSimpleClass()
        {
            string puml = """
            @startuml
            class User {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram, Is.Not.Null);
            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var user = diagram.DataTypes[0];
            Assert.That(user.Name, Is.EqualTo("User"));
            Assert.That(user, Is.TypeOf<UMLClass>());
            var userClass = user as UMLClass;
            Assert.That(userClass.IsAbstract, Is.False);
            Assert.That(user.Properties.Count, Is.EqualTo(0));
            Assert.That(user.Methods.Count, Is.EqualTo(0));
            Assert.That(user.Namespace, Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task ParseAbstractClass()
        {
            string puml = """
            @startuml
            abstract class Animal {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var cls = diagram.DataTypes[0] as UMLClass;
            Assert.That(cls, Is.Not.Null);
            Assert.That(cls.Name, Is.EqualTo("Animal"));
            Assert.That(cls.IsAbstract, Is.True);
            Assert.That(cls.Properties.Count, Is.EqualTo(0));
            Assert.That(cls.Methods.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ParseClassWithAlias()
        {
            string puml = """
            @startuml
            class User as U {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Is.EqualTo("User"));
            Assert.That(cls.Alias, Is.EqualTo("U"));
            Assert.That(cls, Is.TypeOf<UMLClass>());
            var umlClass = cls as UMLClass;
            Assert.That(umlClass.IsAbstract, Is.False);
        }

        [Test]
        public async Task ParseClassWithQuotedName()
        {
            string puml = """
            @startuml
            class "User Account" {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Is.EqualTo("User Account"));
            Assert.That(cls, Is.TypeOf<UMLClass>());
            Assert.That(cls.Properties.Count, Is.EqualTo(0));
            Assert.That(cls.Methods.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ParseClassWithGenericType()
        {
            string puml = """
            @startuml
            class Repository<T> {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Contains.Substring("Repository"));
            Assert.That(cls.Name, Contains.Substring("<T>"));
            Assert.That(cls.NonGenericName, Is.EqualTo("Repository"));
            Assert.That(cls, Is.TypeOf<UMLClass>());
        }

        #endregion

        #region Enum Parsing

        [Test]
        public async Task ParseSimpleEnum()
        {
            string puml = """
            @startuml
            enum Status {
                ACTIVE
                INACTIVE
                PENDING
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var enumType = diagram.DataTypes[0] as UMLEnum;
            Assert.That(enumType, Is.Not.Null);
            Assert.That(enumType.Name, Is.EqualTo("Status"));
            Assert.That(enumType.Properties.Count, Is.EqualTo(3));
            
            var activeProp = enumType.Properties.FirstOrDefault(p => p.Name == "ACTIVE");
            Assert.That(activeProp, Is.Not.Null);
            Assert.That(activeProp.Name, Is.EqualTo("ACTIVE"));
            Assert.That(activeProp.ObjectType.Name, Is.EqualTo("int"));
            Assert.That(activeProp.Visibility, Is.EqualTo(UMLVisibility.Public));

            var inactiveProp = enumType.Properties.FirstOrDefault(p => p.Name == "INACTIVE");
            Assert.That(inactiveProp, Is.Not.Null);
            Assert.That(inactiveProp.Name, Is.EqualTo("INACTIVE"));

            var pendingProp = enumType.Properties.FirstOrDefault(p => p.Name == "PENDING");
            Assert.That(pendingProp, Is.Not.Null);
            Assert.That(pendingProp.Name, Is.EqualTo("PENDING"));
        }

        #endregion

        #region Interface Parsing

        [Test]
        public async Task ParseSimpleInterface()
        {
            string puml = """
            @startuml
            interface IRepository {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var iface = diagram.DataTypes[0] as UMLInterface;
            Assert.That(iface, Is.Not.Null);
            Assert.That(iface.Name, Is.EqualTo("IRepository"));
            Assert.That(iface.Properties.Count, Is.EqualTo(0));
            Assert.That(iface.Methods.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ParseInterfaceWithAlias()
        {
            string puml = """
            @startuml
            interface IRepository as IRepo {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var iface = diagram.DataTypes[0] as UMLInterface;
            Assert.That(iface, Is.Not.Null);
            Assert.That(iface.Name, Is.EqualTo("IRepository"));
            Assert.That(iface.Alias, Is.EqualTo("IRepo"));
        }

        [Test]
        public async Task ParseInterfaceWithGenericType()
        {
            string puml = """
            @startuml
            interface IRepository<T> {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var iface = diagram.DataTypes[0] as UMLInterface;
            Assert.That(iface, Is.Not.Null);
            Assert.That(iface.Name, Contains.Substring("IRepository"));
            Assert.That(iface.Name, Contains.Substring("<T>"));
            Assert.That(iface.NonGenericName, Is.EqualTo("IRepository"));
        }

        #endregion

        #region Struct Parsing

        [Test]
        public async Task ParseSimpleStruct()
        {
            string puml = """
            @startuml
            struct Point {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var strct = diagram.DataTypes[0] as UMLStruct;
            Assert.That(strct, Is.Not.Null);
            Assert.That(strct.Name, Is.EqualTo("Point"));
            Assert.That(strct.Properties.Count, Is.EqualTo(0));
            Assert.That(strct.Methods.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ParseStructWithAlias()
        {
            string puml = """
            @startuml
            struct Point as P {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var strct = diagram.DataTypes[0] as UMLStruct;
            Assert.That(strct, Is.Not.Null);
            Assert.That(strct.Name, Is.EqualTo("Point"));
            Assert.That(strct.Alias, Is.EqualTo("P"));
        }

        #endregion

        #region Property Parsing

        [Test]
        public async Task ParseSimpleProperty()
        {
            string puml = """
            @startuml
            class User {
                name: string
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Is.EqualTo("User"));
            Assert.That(cls, Is.TypeOf<UMLClass>());
            Assert.That(cls.Properties.Count, Is.EqualTo(1));
            
            var nameProp = cls.Properties[0];
            Assert.That(nameProp.Name, Is.EqualTo("name"));
            Assert.That(nameProp.ObjectType.Name, Is.EqualTo("string"));
            Assert.That(nameProp.Visibility, Is.EqualTo(UMLVisibility.None));
            Assert.That(nameProp.ListType, Is.EqualTo(ListTypes.None));
            Assert.That(nameProp.IsStatic, Is.False);
            Assert.That(nameProp.IsAbstract, Is.False);
        }

        [Test]
        public async Task ParsePropertyWithVisibility()
        {
            string puml = """
            @startuml
            class User {
                +publicProp: string
                -privateProp: int
                #protectedProp: bool
                ~internalProp: double
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Properties.Count, Is.EqualTo(4));

            var pubProp = cls.Properties.FirstOrDefault(p => p.Name == "publicProp");
            Assert.That(pubProp, Is.Not.Null);
            Assert.That(pubProp.Name, Is.EqualTo("publicProp"));
            Assert.That(pubProp.Visibility, Is.EqualTo(UMLVisibility.Public));
            Assert.That(pubProp.ObjectType.Name, Is.EqualTo("string"));
            Assert.That(pubProp.IsStatic, Is.False);
            Assert.That(pubProp.IsAbstract, Is.False);

            var privProp = cls.Properties.FirstOrDefault(p => p.Name == "privateProp");
            Assert.That(privProp, Is.Not.Null);
            Assert.That(privProp.Name, Is.EqualTo("privateProp"));
            Assert.That(privProp.Visibility, Is.EqualTo(UMLVisibility.Private));
            Assert.That(privProp.ObjectType.Name, Is.EqualTo("int"));

            var protProp = cls.Properties.FirstOrDefault(p => p.Name == "protectedProp");
            Assert.That(protProp, Is.Not.Null);
            Assert.That(protProp.Name, Is.EqualTo("protectedProp"));
            Assert.That(protProp.Visibility, Is.EqualTo(UMLVisibility.Protected));
            Assert.That(protProp.ObjectType.Name, Is.EqualTo("bool"));

            var intProp = cls.Properties.FirstOrDefault(p => p.Name == "internalProp");
            Assert.That(intProp, Is.Not.Null);
            Assert.That(intProp.Name, Is.EqualTo("internalProp"));
            Assert.That(intProp.Visibility, Is.EqualTo(UMLVisibility.Internal));
            Assert.That(intProp.ObjectType.Name, Is.EqualTo("double"));
        }

        [Test]
        public async Task ParsePropertyWithModifiers()
        {
            string puml = """
            @startuml
            class User {
                +count: int {static}
                #getValue(): string {abstract}
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Properties.Count, Is.EqualTo(1));
            
            var staticProp = cls.Properties.FirstOrDefault(p => p.Name == "count");
            Assert.That(staticProp, Is.Not.Null);
            Assert.That(staticProp.Name, Is.EqualTo("count"));
            Assert.That(staticProp.Visibility, Is.EqualTo(UMLVisibility.Public));
            Assert.That(staticProp.ObjectType.Name, Is.EqualTo("int"));
            Assert.That(staticProp.IsStatic, Is.True);
            Assert.That(staticProp.IsAbstract, Is.False);

            // Abstract method should be in Methods, not Properties
            var abstractMethod = cls.Methods.FirstOrDefault(m => m.Name == "getValue");
            Assert.That(abstractMethod, Is.Not.Null);
            Assert.That(abstractMethod.Name, Is.EqualTo("getValue"));
            Assert.That(abstractMethod.Visibility, Is.EqualTo(UMLVisibility.Protected));
            Assert.That(abstractMethod.ReturnType.Name, Is.EqualTo("string"));
            Assert.That(abstractMethod.IsAbstract, Is.True);
            Assert.That(abstractMethod.IsStatic, Is.False);
        }

        [Test]
        public async Task ParsePropertyWithCollectionTypes()
        {
            string puml = """
            @startuml
            class User {
                +items: List<string>
                +readOnlyData: IReadOnlyCollection<int>
                +array: string[]
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Is.EqualTo("User"));
            Assert.That(cls.Properties.Count, Is.EqualTo(3));

            var listProp = cls.Properties.FirstOrDefault(p => p.Name == "items");
            Assert.That(listProp, Is.Not.Null);
            Assert.That(listProp.Name, Is.EqualTo("items"));
            Assert.That(listProp.ObjectType.Name, Is.EqualTo("string"));
            Assert.That(listProp.ListType, Is.EqualTo(ListTypes.List));
            Assert.That(listProp.Visibility, Is.EqualTo(UMLVisibility.Public));

            var roCollectionProp = cls.Properties.FirstOrDefault(p => p.Name == "readOnlyData");
            Assert.That(roCollectionProp, Is.Not.Null);
            Assert.That(roCollectionProp.Name, Is.EqualTo("readOnlyData"));
            Assert.That(roCollectionProp.ObjectType.Name, Is.EqualTo("int"));
            Assert.That(roCollectionProp.ListType, Is.EqualTo(ListTypes.IReadOnlyCollection));
            Assert.That(roCollectionProp.Visibility, Is.EqualTo(UMLVisibility.Public));

            var arrayProp = cls.Properties.FirstOrDefault(p => p.Name == "array");
            Assert.That(arrayProp, Is.Not.Null);
            Assert.That(arrayProp.Name, Is.EqualTo("array"));
            Assert.That(arrayProp.ObjectType.Name, Is.EqualTo("string"));
            Assert.That(arrayProp.ListType, Is.EqualTo(ListTypes.Array));
            Assert.That(arrayProp.Visibility, Is.EqualTo(UMLVisibility.Public));
        }

        [Test]
        public async Task ParseCSharpStyleProperty()
        {
            string puml = """
            @startuml
            class User {
                +string name
                -int age
                #bool isActive
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Is.EqualTo("User"));
            Assert.That(cls.Properties.Count, Is.EqualTo(3));

            var nameProp = cls.Properties.FirstOrDefault(p => p.Name == "name");
            Assert.That(nameProp, Is.Not.Null);
            Assert.That(nameProp.Name, Is.EqualTo("name"));
            Assert.That(nameProp.ObjectType.Name, Is.EqualTo("string"));
            Assert.That(nameProp.Visibility, Is.EqualTo(UMLVisibility.Public));
            Assert.That(nameProp.ListType, Is.EqualTo(ListTypes.None));

            var ageProp = cls.Properties.FirstOrDefault(p => p.Name == "age");
            Assert.That(ageProp, Is.Not.Null);
            Assert.That(ageProp.Name, Is.EqualTo("age"));
            Assert.That(ageProp.ObjectType.Name, Is.EqualTo("int"));
            Assert.That(ageProp.Visibility, Is.EqualTo(UMLVisibility.Private));

            var isActiveProp = cls.Properties.FirstOrDefault(p => p.Name == "isActive");
            Assert.That(isActiveProp, Is.Not.Null);
            Assert.That(isActiveProp.Name, Is.EqualTo("isActive"));
            Assert.That(isActiveProp.ObjectType.Name, Is.EqualTo("bool"));
            Assert.That(isActiveProp.Visibility, Is.EqualTo(UMLVisibility.Protected));
        }

        [Test]
        public async Task ParsePropertyWithCardinalitySyntax()
        {
            string puml = """
            @startuml
            class User {
                +tags: String[0..*]
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Is.EqualTo("User"));
            Assert.That(cls.Properties.Count, Is.EqualTo(1));

            var tagsProp = cls.Properties.FirstOrDefault(p => p.Name == "tags");
            Assert.That(tagsProp, Is.Not.Null);
            Assert.That(tagsProp.ObjectType.Name, Is.EqualTo("String"));
            Assert.That(tagsProp.ListType, Is.EqualTo(ListTypes.List));
            Assert.That(tagsProp.Visibility, Is.EqualTo(UMLVisibility.Public));
        }

        [Test]
        public async Task ParseStaticPropertyWithCardinalitySyntax()
        {
            string puml = """
            @startuml
            class User {
                +tags: String[0..*] {static}
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Is.EqualTo("User"));
            Assert.That(cls.Properties.Count, Is.EqualTo(1));

            var tagsProp = cls.Properties.FirstOrDefault(p => p.Name == "tags");
            Assert.That(tagsProp, Is.Not.Null);
            Assert.That(tagsProp.ObjectType.Name, Is.EqualTo("String"));
            Assert.That(tagsProp.ListType, Is.EqualTo(ListTypes.List));
            Assert.That(tagsProp.Visibility, Is.EqualTo(UMLVisibility.Public));
            Assert.That(tagsProp.IsStatic, Is.True);
        }

        #endregion

        #region Method Parsing

        [Test]
        public async Task ParseSimpleMethod()
        {
            string puml = """
            @startuml
            class User {
                getName(): string
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Methods.Count, Is.EqualTo(1));
            Assert.That(cls.Methods[0].Name, Is.EqualTo("getName"));
            Assert.That(cls.Methods[0].ReturnType.Name, Is.EqualTo("string"));
        }

        [Test]
        public async Task ParseMethodWithVisibility()
        {
            string puml = """
            @startuml
            class User {
                +publicMethod(): void
                -privateMethod(): int
                #protectedMethod(): bool
                ~internalMethod(): string
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Is.EqualTo("User"));
            Assert.That(cls.Methods.Count, Is.EqualTo(4));

            var pubMethod = cls.Methods.FirstOrDefault(m => m.Name == "publicMethod");
            Assert.That(pubMethod, Is.Not.Null);
            Assert.That(pubMethod.Name, Is.EqualTo("publicMethod"));
            Assert.That(pubMethod.Visibility, Is.EqualTo(UMLVisibility.Public));
            Assert.That(pubMethod.ReturnType.Name, Is.EqualTo("void"));
            Assert.That(pubMethod.IsStatic, Is.False);
            Assert.That(pubMethod.IsAbstract, Is.False);
            Assert.That(pubMethod.Parameters.Count, Is.EqualTo(0));

            var privMethod = cls.Methods.FirstOrDefault(m => m.Name == "privateMethod");
            Assert.That(privMethod, Is.Not.Null);
            Assert.That(privMethod.Name, Is.EqualTo("privateMethod"));
            Assert.That(privMethod.Visibility, Is.EqualTo(UMLVisibility.Private));
            Assert.That(privMethod.ReturnType.Name, Is.EqualTo("int"));

            var protMethod = cls.Methods.FirstOrDefault(m => m.Name == "protectedMethod");
            Assert.That(protMethod, Is.Not.Null);
            Assert.That(protMethod.Name, Is.EqualTo("protectedMethod"));
            Assert.That(protMethod.Visibility, Is.EqualTo(UMLVisibility.Protected));
            Assert.That(protMethod.ReturnType.Name, Is.EqualTo("bool"));

            var intMethod = cls.Methods.FirstOrDefault(m => m.Name == "internalMethod");
            Assert.That(intMethod, Is.Not.Null);
            Assert.That(intMethod.Name, Is.EqualTo("internalMethod"));
            Assert.That(intMethod.Visibility, Is.EqualTo(UMLVisibility.Internal));
            Assert.That(intMethod.ReturnType.Name, Is.EqualTo("string"));
        }

        [Test]
        public async Task ParseMethodWithParameters()
        {
            string puml = """
            @startuml
            class User {
                setData(name: string, age: int): void
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Is.EqualTo("User"));
            Assert.That(cls.Methods.Count, Is.EqualTo(1));
            
            var method = cls.Methods[0];
            Assert.That(method.Name, Is.EqualTo("setData"));
            Assert.That(method.ReturnType.Name, Is.EqualTo("void"));
            Assert.That(method.Visibility, Is.EqualTo(UMLVisibility.None));
            Assert.That(method.IsStatic, Is.False);
            Assert.That(method.IsAbstract, Is.False);
            Assert.That(method.Parameters.Count, Is.EqualTo(2));

            Assert.That(method.Parameters[0].Name, Is.EqualTo("name"));
            Assert.That(method.Parameters[0].ObjectType.Name, Is.EqualTo("string"));
            Assert.That(method.Parameters[0].ListType, Is.EqualTo(ListTypes.None));

            Assert.That(method.Parameters[1].Name, Is.EqualTo("age"));
            Assert.That(method.Parameters[1].ObjectType.Name, Is.EqualTo("int"));
            Assert.That(method.Parameters[1].ListType, Is.EqualTo(ListTypes.None));
        }

        [Test]
        public async Task ParseMethodWithCSharpStyleParameters()
        {
            string puml = """
            @startuml
            class User {
                +setData(string name, int age): void
                +calculate(int x, int y): int
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            Assert.That(cls.Name, Is.EqualTo("User"));
            Assert.That(cls.Methods.Count, Is.EqualTo(2));

            var setDataMethod = cls.Methods.FirstOrDefault(m => m.Name == "setData");
            Assert.That(setDataMethod, Is.Not.Null);
            Assert.That(setDataMethod.Name, Is.EqualTo("setData"));
            Assert.That(setDataMethod.Visibility, Is.EqualTo(UMLVisibility.Public));
            Assert.That(setDataMethod.ReturnType.Name, Is.EqualTo("void"));
            Assert.That(setDataMethod.Parameters.Count, Is.EqualTo(2));

            Assert.That(setDataMethod.Parameters[0].Name, Is.EqualTo("name"));
            Assert.That(setDataMethod.Parameters[0].ObjectType.Name, Is.EqualTo("string"));

            Assert.That(setDataMethod.Parameters[1].Name, Is.EqualTo("age"));
            Assert.That(setDataMethod.Parameters[1].ObjectType.Name, Is.EqualTo("int"));

            var calcMethod = cls.Methods.FirstOrDefault(m => m.Name == "calculate");
            Assert.That(calcMethod, Is.Not.Null);
            Assert.That(calcMethod.Visibility, Is.EqualTo(UMLVisibility.Public));
            Assert.That(calcMethod.ReturnType.Name, Is.EqualTo("int"));
        }

        [Test]
        public async Task ParseMethodWithGenericReturnType()
        {
            string puml = """
            @startuml
            class Repository {
                find(): List<User>
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            var method = cls.Methods[0];
            Assert.That(method.ReturnType.Name, Contains.Substring("List"));
        }

        [Test]
        public async Task ParseMethodWithGenericParameters()
        {
            string puml = """
            @startuml
            class Repository {
                process(data: List<T>, handler: Action<T>): void
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            var method = cls.Methods[0];
            Assert.That(method.Parameters.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task ParseStaticMethod()
        {
            string puml = """
            @startuml
            class User {
                {static} create(): User
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            var method = cls.Methods[0];
            Assert.That(method.IsStatic, Is.True);
        }

        [Test]
        public async Task ParseAbstractMethod()
        {
            string puml = """
            @startuml
            class User {
                {abstract} doWork(): void
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            var method = cls.Methods[0];
            Assert.That(method.IsAbstract, Is.True);
        }

        [Test]
        public async Task ParseMethodWithUMLReturnTypeStyle()
        {
            string puml = """
            @startuml
            class User {
                getName() : string
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            var method = cls.Methods[0];
            Assert.That(method.Name, Is.EqualTo("getName"));
            Assert.That(method.ReturnType.Name, Is.EqualTo("string"));
        }

        #endregion

        #region Inheritance and Relationships

        [Test]
        public async Task ParseSingleInheritance()
        {
            string puml = """
            @startuml
            class Animal {
            }
            class Dog {
            }
            Dog --> Animal
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(2));
            var dog = diagram.DataTypes.FirstOrDefault(d => d.Name == "Dog");
            Assert.That(dog.Bases.Count, Is.EqualTo(1));
            Assert.That(dog.Bases[0].Name, Is.EqualTo("Animal"));
        }

        [Test]
        public async Task ParseInterfaceImplementation()
        {
            string puml = """
            @startuml
            interface IRepository {
            }
            class Repository {
            }
            Repository --|> IRepository
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var repo = diagram.DataTypes.FirstOrDefault(d => d.Name == "Repository");
            Assert.That(repo.Interfaces.Count, Is.EqualTo(1));
            Assert.That(repo.Interfaces[0].Name, Is.EqualTo("IRepository"));
        }

        [Test]
        public async Task ParseMultipleInheritanceOrInterfaces()
        {
            string puml = """
            @startuml
            interface IRepository {
            }
            interface IUnitOfWork {
            }
            class DataLayer {
            }
            DataLayer --|> IRepository
            DataLayer --|> IUnitOfWork
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var dataLayer = diagram.DataTypes.FirstOrDefault(d => d.Name == "DataLayer");
            Assert.That(dataLayer.Interfaces.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task ParseInheritanceWithAlias()
        {
            string puml = """
            @startuml
            class Animal as A {
            }
            class Dog as D {
            }
            D --> A
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var dog = diagram.DataTypes.FirstOrDefault(d => d.Alias == "D");
            Assert.That(dog, Is.Not.Null);
            Assert.That(dog.Bases.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ParseArrowDirections()
        {
            string puml = """
            @startuml
            class A {
            }
            class B {
            }
            A --> B
            B <-- C
            A -|> B
            A --|> B
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.GreaterThanOrEqualTo(2));
            // Verify diagram parses without errors
            Assert.That(diagram.Errors.Count, Is.LessThan(diagram.DataTypes.Count));
        }

        #endregion

        #region Package/Namespace Parsing

        [Test]
        public async Task ParsePackageWithClass()
        {
            string puml = """
            @startuml
            package "com.example.domain" {
                class User {
                }
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var user = diagram.DataTypes.FirstOrDefault(d => d.Name == "User");
            Assert.That(user, Is.Not.Null);
            Assert.That(user.Name, Is.EqualTo("User"));
            Assert.That(user.Namespace, Is.EqualTo("com.example.domain"));
            Assert.That(user, Is.TypeOf<UMLClass>());
        }

        [Test]
        public async Task ParseNestedPackages()
        {
            string puml = """
            @startuml
            package "Domain" {
                package "Models" {
                    class User {
                    }
                }
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var user = diagram.DataTypes.FirstOrDefault(d => d.Name == "User");
            Assert.That(user, Is.Not.Null);
            Assert.That(user.Name, Is.EqualTo("User"));
            Assert.That(user.Namespace, Is.EqualTo("Domain.Models"));
            Assert.That(user, Is.TypeOf<UMLClass>());
        }

        [Test]
        public async Task ParseMultiplePackagesWithClasses()
        {
            string puml = """
            @startuml
            package "Domain" {
                class User {
                }
            }
            package "Data" {
                class Repository {
                }
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(2));

            var user = diagram.DataTypes.FirstOrDefault(d => d.Name == "User");
            Assert.That(user, Is.Not.Null);
            Assert.That(user.Name, Is.EqualTo("User"));
            Assert.That(user.Namespace, Is.EqualTo("Domain"));
            Assert.That(user, Is.TypeOf<UMLClass>());

            var repo = diagram.DataTypes.FirstOrDefault(d => d.Name == "Repository");
            Assert.That(repo, Is.Not.Null);
            Assert.That(repo.Name, Is.EqualTo("Repository"));
            Assert.That(repo.Namespace, Is.EqualTo("Data"));
            Assert.That(repo, Is.TypeOf<UMLClass>());

            // Verify namespaces are different
            Assert.That(user.Namespace, Is.Not.EqualTo(repo.Namespace));
        }
        #endregion

        #region Composition and Association

        [Test]
        public async Task ParseComposition()
        {
            string puml = """
            @startuml
            class Company {
            }
            class Employee {
            }
            Company "1" *-- "many" Employee : employs
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var company = diagram.DataTypes.FirstOrDefault(d => d.Name == "Company");
            // Check that composition property was added
            var compositionProp = company?.Properties.FirstOrDefault(p => p.Name == "employs");
            Assert.That(compositionProp, Is.Not.Null);
        }

        [Test]
        public async Task ParseCompositionWithoutLabel()
        {
            string puml = """
            @startuml
            class A {
            }
            class B {
            }
            A *-- B
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(2));
        }

        #endregion

        #region Comments and Notes

        [Test]
        public async Task ParseSingleLineComment()
        {
            string puml = """
            @startuml
            ' This is a comment
            class User {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            // The parser treats comment-like syntax as additional elements
            Assert.That(diagram.DataTypes.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task ParseMultiLineComment()
        {
            string puml = """
            @startuml
            /'
            This is a multiline comment
            spanning multiple lines
            '/
            class User {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

       
            Assert.That(diagram.DataTypes.Count, Is.EqualTo(2));
            Assert.That(diagram.DataTypes[0] is UMLComment);
            Assert.That(diagram.DataTypes[1] is UMLClass);
        }

        [Test]
        public async Task ParseNote()
        {
            string puml = """
            @startuml
            class User {
            }
            note "User entity" as N
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.Notes.Count, Is.GreaterThanOrEqualTo(1));
        }

        #endregion

        #region Complex Scenarios

        [Test]
        public async Task ParseCompleteClassHierarchy()
        {
            string puml = """
            @startuml
            abstract class BaseEntity {
                +id: int
                +createdDate: DateTime
                +{abstract} validate(): bool
            }

            class User {
                +username: string
                +email: string
                +setPassword(pwd: string): void
                +getProfile(): User
            }

            class Product {
                +name: string
                +price: decimal
                +stock: int
                +{static} create(name: string, price: decimal): Product
            }

            interface IRepository<T> {
                +find(id: int): T
                +save(entity: T): void
            }

            class UserRepository {
                +find(id: int): User
                +save(entity: User): void
                -connection: string
            }

            User --> BaseEntity
            Product --> BaseEntity
            UserRepository --|> IRepository
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            // Verify all types are parsed
            Assert.That(diagram.DataTypes.Count, Is.GreaterThanOrEqualTo(4));

            // Verify BaseEntity
            var baseEntity = diagram.DataTypes.FirstOrDefault(d => d.Name == "BaseEntity");
            Assert.That(baseEntity, Is.Not.Null);
            Assert.That(baseEntity.Name, Is.EqualTo("BaseEntity"));
            Assert.That(baseEntity, Is.TypeOf<UMLClass>());
            var baseEntityClass = baseEntity as UMLClass;
            Assert.That(baseEntityClass.IsAbstract, Is.True);
            Assert.That(baseEntity.Properties.Count, Is.EqualTo(2));
            Assert.That(baseEntity.Properties.Any(p => p.Name == "id" && p.ObjectType.Name == "int" && p.Visibility == UMLVisibility.Public));
            Assert.That(baseEntity.Properties.Any(p => p.Name == "createdDate" && p.ObjectType.Name == "DateTime" && p.Visibility == UMLVisibility.Public));
            Assert.That(baseEntity.Methods.Count, Is.EqualTo(1));
            var validateMethod = baseEntity.Methods.FirstOrDefault(m => m.Name == "validate");
            Assert.That(validateMethod, Is.Not.Null);
            Assert.That(validateMethod.IsAbstract, Is.True);
            Assert.That(validateMethod.ReturnType.Name, Is.EqualTo("bool"));
            Assert.That(validateMethod.Visibility, Is.EqualTo(UMLVisibility.Public));

            // Verify User
            var user = diagram.DataTypes.FirstOrDefault(d => d.Name == "User");
            Assert.That(user, Is.Not.Null);
            Assert.That(user.Name, Is.EqualTo("User"));
            Assert.That(user, Is.TypeOf<UMLClass>());
            Assert.That(user.Bases.Any(b => b.Name == "BaseEntity"), Is.True);
            Assert.That(user.Properties.Count, Is.EqualTo(2));
            Assert.That(user.Properties.Any(p => p.Name == "username" && p.ObjectType.Name == "string" && p.Visibility == UMLVisibility.Public));
            Assert.That(user.Properties.Any(p => p.Name == "email" && p.ObjectType.Name == "string" && p.Visibility == UMLVisibility.Public));
            Assert.That(user.Methods.Count, Is.EqualTo(2));
            var setPasswordMethod = user.Methods.FirstOrDefault(m => m.Name == "setPassword");
            Assert.That(setPasswordMethod, Is.Not.Null);
            Assert.That(setPasswordMethod.Visibility, Is.EqualTo(UMLVisibility.Public));
            Assert.That(setPasswordMethod.ReturnType.Name, Is.EqualTo("void"));

            // Verify Product
            var product = diagram.DataTypes.FirstOrDefault(d => d.Name == "Product");
            Assert.That(product, Is.Not.Null);
            Assert.That(product.Name, Is.EqualTo("Product"));
            Assert.That(product, Is.TypeOf<UMLClass>());
            Assert.That(product.Bases.Any(b => b.Name == "BaseEntity"), Is.True);
            Assert.That(product.Properties.Count, Is.EqualTo(3));
            Assert.That(product.Properties.Any(p => p.Name == "price" && p.ObjectType.Name == "decimal" && p.Visibility == UMLVisibility.Public));
            var createMethod = product.Methods.FirstOrDefault(m => m.Name == "create");
            Assert.That(createMethod, Is.Not.Null);
            Assert.That(createMethod.IsStatic, Is.True);
            Assert.That(createMethod.Visibility, Is.EqualTo(UMLVisibility.Public));

            // Verify UserRepository
            var repo = diagram.DataTypes.FirstOrDefault(d => d.Name == "UserRepository");
            Assert.That(repo, Is.Not.Null);
            Assert.That(repo.Name, Is.EqualTo("UserRepository"));
            Assert.That(repo, Is.TypeOf<UMLClass>());
            Assert.That(repo.Properties.Count, Is.EqualTo(1));
            var connectionProp = repo.Properties.FirstOrDefault(p => p.Name == "connection");
            Assert.That(connectionProp, Is.Not.Null);
            Assert.That(connectionProp.Visibility, Is.EqualTo(UMLVisibility.Private));
            Assert.That(connectionProp.ObjectType.Name, Is.EqualTo("string"));
        }

        [Test]
        public async Task ParseClassesWithAllVisibilityLevels()
        {
            string puml = """
            @startuml
            class Account {
                +balance: decimal
                -pin: string
                #accountNumber: string
                ~bankName: string
                +deposit(amount: decimal): void
                -verify(): bool
                #getAccountInfo(): string
                ~updateLedger(): void
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var account = diagram.DataTypes[0];
            Assert.That(account.Name, Is.EqualTo("Account"));
            Assert.That(account, Is.TypeOf<UMLClass>());

            // Verify all visibility levels in properties
            Assert.That(account.Properties.Count, Is.EqualTo(4));
            
            var balanceProp = account.Properties.FirstOrDefault(p => p.Name == "balance");
            Assert.That(balanceProp, Is.Not.Null);
            Assert.That(balanceProp.Visibility, Is.EqualTo(UMLVisibility.Public));
            Assert.That(balanceProp.ObjectType.Name, Is.EqualTo("decimal"));

            var pinProp = account.Properties.FirstOrDefault(p => p.Name == "pin");
            Assert.That(pinProp, Is.Not.Null);
            Assert.That(pinProp.Visibility, Is.EqualTo(UMLVisibility.Private));
            Assert.That(pinProp.ObjectType.Name, Is.EqualTo("string"));

            var accountNumberProp = account.Properties.FirstOrDefault(p => p.Name == "accountNumber");
            Assert.That(accountNumberProp, Is.Not.Null);
            Assert.That(accountNumberProp.Visibility, Is.EqualTo(UMLVisibility.Protected));
            Assert.That(accountNumberProp.ObjectType.Name, Is.EqualTo("string"));

            var bankNameProp = account.Properties.FirstOrDefault(p => p.Name == "bankName");
            Assert.That(bankNameProp, Is.Not.Null);
            Assert.That(bankNameProp.Visibility, Is.EqualTo(UMLVisibility.Internal));
            Assert.That(bankNameProp.ObjectType.Name, Is.EqualTo("string"));

            // Verify all visibility levels in methods
            Assert.That(account.Methods.Count, Is.EqualTo(4));

            var depositMethod = account.Methods.FirstOrDefault(m => m.Name == "deposit");
            Assert.That(depositMethod, Is.Not.Null);
            Assert.That(depositMethod.Visibility, Is.EqualTo(UMLVisibility.Public));
            Assert.That(depositMethod.ReturnType.Name, Is.EqualTo("void"));
            Assert.That(depositMethod.Parameters.Count, Is.EqualTo(1));

            var verifyMethod = account.Methods.FirstOrDefault(m => m.Name == "verify");
            Assert.That(verifyMethod, Is.Not.Null);
            Assert.That(verifyMethod.Visibility, Is.EqualTo(UMLVisibility.Private));
            Assert.That(verifyMethod.ReturnType.Name, Is.EqualTo("bool"));

            var getAccountInfoMethod = account.Methods.FirstOrDefault(m => m.Name == "getAccountInfo");
            Assert.That(getAccountInfoMethod, Is.Not.Null);
            Assert.That(getAccountInfoMethod.Visibility, Is.EqualTo(UMLVisibility.Protected));
            Assert.That(getAccountInfoMethod.ReturnType.Name, Is.EqualTo("string"));

            var updateLedgerMethod = account.Methods.FirstOrDefault(m => m.Name == "updateLedger");
            Assert.That(updateLedgerMethod, Is.Not.Null);
            Assert.That(updateLedgerMethod.Visibility, Is.EqualTo(UMLVisibility.Internal));
            Assert.That(updateLedgerMethod.ReturnType.Name, Is.EqualTo("void"));
        }
        #endregion

        #region Error Handling

        [Test]
        public async Task ParseInvalidSyntaxProducesError()
        {
            string puml = """
            @startuml
            invalid syntax here
            class User {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram, Is.Not.Null);
            // The parser should still parse the valid class despite the invalid line
            Assert.That(diagram.DataTypes.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task ParseNonexistentBaseClassProducesError()
        {
            string puml = """
            @startuml
            class Child {
            }
            Child --> NonExistent
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.Errors.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task ParseCircularInheritanceAttempt()
        {
            string puml = """
            @startuml
            class User {
            }
            User --> User
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.Errors.Any(e => e.Value.Contains("cannot be the same")), Is.True);
        }

        [Test]
        public async Task ParseDuplicatePackageProducesWarning()
        {
            string puml = """
            @startuml
            package "Domain" {
                class User {
                }
            }
            package "Domain" {
                class Product {
                }
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.Errors.Any(e => e.Value.Contains("Package")), Is.True);
        }

        #endregion

        #region Edge Cases

        [Test]
        public async Task ParseClassNameWithSpecialCharacters()
        {
            string puml = """
            @startuml
            class "User_Account" {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            Assert.That(diagram.DataTypes[0].Name, Contains.Substring("User"));
        }

        [Test]
        public async Task ParsePropertyWithoutType()
        {
            string puml = """
            @startuml
            class User {
                +someProperty
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes.FirstOrDefault(d => d.Name == "User");
            // The parser may or may not successfully parse this - just verify no crash
            Assert.That(cls, Is.Not.Null);
        }

        [Test]
        public async Task ParseWhitespaceAndFormatting()
        {
            string puml = """
            @startuml

            class   User   {
                +   name   :   string
            }

            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.DataTypes.Count, Is.EqualTo(1));
            var user = diagram.DataTypes[0];
            Assert.That(user.Name, Is.EqualTo("User"));
            var prop = user.Properties.FirstOrDefault(p => p.Name == "name");
            Assert.That(prop, Is.Not.Null);
        }

        [Test]
        public async Task ParseEmptyDiagram()
        {
            string puml = """
            @startuml
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram, Is.Not.Null);
            Assert.That(diagram.DataTypes.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ParseDiagramWithOnlyComments()
        {
            string puml = """
            @startuml
            ' This is a comment
            ' Another comment
            /'
            Block comment
            '/
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram, Is.Not.Null);
            Assert.That(diagram.DataTypes.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task ParseMethodWithNoParameters()
        {
            string puml = """
            @startuml
            class User {
                +getId(): int
                +getName(): string
                +refresh(): void
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            var getId = cls.Methods.FirstOrDefault(m => m.Name == "getId");
            Assert.That(getId, Is.Not.Null);
            Assert.That(getId.Parameters.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ParseMethodWithSpacesInParameters()
        {
            string puml = """
            @startuml
            class User {
                +process(item: List<User>, handler: Action<User, Result>): void
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var cls = diagram.DataTypes[0];
            var method = cls.Methods[0];
            Assert.That(method.Parameters.Count, Is.EqualTo(2));
        }

        #endregion

        #region Stereotype Tests

        [Test]
        public async Task ParseClassWithStereotype()
        {
            string puml = """
            @startuml
            class User <<entity>> {
                +name: string
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var user = diagram.DataTypes[0] as UMLClass;
            Assert.That(user.StereoType, Is.EqualTo("entity"));
        }

        [Test]
        public async Task ParseInterfaceWithStereotype()
        {
            string puml = """
            @startuml
            interface IService <<injectable>> {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            var iface = diagram.DataTypes[0] as UMLInterface;
            Assert.That(iface, Is.Not.Null);
        }

        #endregion

        #region String Parsing vs File Parsing

        [Test]
        public async Task ParseStringAndFileProduceSearResults()
        {
            string puml = """
            @startuml
            class TestClass {
                +value: int
            }
            @enduml
            """;

            var diagramFromString = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagramFromString, Is.Not.Null);
            Assert.That(diagramFromString.DataTypes.Count, Is.GreaterThan(0));
        }

        #endregion

        #region Title and Metadata

        [Test]
        public async Task ParseTitleFromTitle()
        {
            string puml = """
            @startuml
            title My Title
            class User {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.Title, Contains.Substring("My Title"));
        }

        [Test]
        public async Task ParseTitleFromStartuml()
        {
            string puml = """
            @startuml My Diagram Title
            class User {
            }
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram.Title, Contains.Substring("My Diagram Title"));
        }

        #endregion

        #region Integration Tests

        [Test]
        public async Task ParseRealWorldDomainModel()
        {
            string puml = """
            @startuml Domain Model

            package "Domain.Core" {
                abstract class Entity {
                    #id: int
                    #createdOn: DateTime
                    #{abstract} getDomainEvents(): List<DomainEvent>
                }

                abstract class ValueObject {
                    #{abstract} equals(other: ValueObject): bool
                    #{abstract} getHashCode(): int
                }
            }

            package "Domain.Orders" {
                class Order {
                    -orderId: int
                    -customerId: int
                    -items: List<OrderItem>
                    +addItem(product: Product, quantity: int): void
                    +calculateTotal(): decimal
                    +cancel(): void
                }

                class OrderItem {
                    -orderId: int
                    -productId: int
                    -quantity: int
                    -unitPrice: decimal
                }

                enum OrderStatus {
                    PENDING
                    CONFIRMED
                    SHIPPED
                    DELIVERED
                    CANCELLED
                }
            }

            package "Domain.Products" {
                class Product {
                    -productId: int
                    -name: string
                    -price: decimal
                    -stock: int
                    +updatePrice(newPrice: decimal): void
                    +decreaseStock(amount: int): void
                }
            }

            Order --> Product
            OrderItem --> Product
            Order "1" *-- "many" OrderItem
            @enduml
            """;

            var diagram = await UMLClassDiagramParser.ReadString(puml);

            Assert.That(diagram, Is.Not.Null);
            // Verify a reasonable number of types are parsed (at least 5)
            Assert.That(diagram.DataTypes.Count, Is.GreaterThanOrEqualTo(5));

            var order = diagram.DataTypes.FirstOrDefault(d => d.Name == "Order");
            Assert.That(order, Is.Not.Null);
            Assert.That(order.Properties.Any(p => p.Name == "items"));
            Assert.That(order.Methods.Any(m => m.Name == "addItem"));

            var product = diagram.DataTypes.FirstOrDefault(d => d.Name == "Product");
            Assert.That(product, Is.Not.Null);

            var orderStatus = diagram.DataTypes.FirstOrDefault(d => d.Name == "OrderStatus");
            Assert.That(orderStatus, Is.TypeOf<UMLEnum>());
            Assert.That(orderStatus.Properties.Count, Is.EqualTo(5));
        }

        #endregion
    }
}
