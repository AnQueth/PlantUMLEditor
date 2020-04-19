namespace UMLModels
{
    public class UMLParameter : UMLSignature
    {
        public UMLParameter()
        {
        }

        public UMLParameter(string name, UMLDataType type, ListTypes listTypes = ListTypes.None)
        {
            Name = name;
            ListType = listTypes;

            ObjectType = type;
        }

        public ListTypes ListType
        {
            get; set;
        }

        public string Name { get; set; }

        public UMLDataType ObjectType { get; set; }

        public override string ToString()
        {
            if (ListType == ListTypes.Array)
                return $"{ObjectType.Name}[] {Name}";
            else if (ListType == ListTypes.IReadOnlyCollection)
            {
                return $"IReadOnlyCollection<{ObjectType.Name}> {Name}";
            }
            else if (ListType == ListTypes.List)
            {
                return $"List<{ObjectType.Name}> {Name}";
            }
            else
            {
                return $"{ObjectType.Name} {Name}";
            }
        }
    }

    public enum ListTypes
    {
        None,
        List,
        Array,
        IReadOnlyCollection
    }
}