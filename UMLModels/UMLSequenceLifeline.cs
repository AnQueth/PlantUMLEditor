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

      

        public UMLSequenceLifeline(string name, string alias, string dataTypeId)
        {
            Text = name;
            DataTypeId = dataTypeId;
            Alias = alias;
        }
    }
}