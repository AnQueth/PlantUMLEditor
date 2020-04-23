namespace UMLModels
{
    public class UMLSequenceLifeline: UMLOrderedEntity
    {
        public string DataTypeId
        {
            get; set;
        }

        public string Alias { get; set; }

        public string Text { get; set; }
        public override string Warning
        {
            get
            {
                return DataTypeId ==  null ? $"{Text} is not a known type for lifeline" : null;
            }
        }


        public UMLSequenceLifeline(string name, string alias, string dataTypeId)
        {
            Text = name;
            DataTypeId = dataTypeId;
            Alias = alias;
        }
    }
}