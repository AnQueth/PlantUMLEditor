namespace UMLModels
{
    public class UMLSequenceLifeline : UMLOrderedEntity
    {
        public UMLSequenceLifeline(string type, string name, string alias, string? dataTypeId, int lineNumber) : base(lineNumber)
        {
            LifeLineType = type;
            Text = name;
            DataTypeId = dataTypeId;
            Alias = alias;
        }

        public string Alias
        {
            get; set;
        }

        public string? DataTypeId
        {
            get; set;
        }

        public bool FreeFormed => LifeLineType != "participant" && LifeLineType != "component";
        public string LifeLineType
        {
            get;
        }
        public string Text
        {
            get; set;
        }

        public override string? Warning
        {
            get
            {
                if (!FreeFormed)
                {
                    return DataTypeId == null ? $" {Text} is not a known type for lifeline" : null;
                }

                return null;
            }
        }
    }
}