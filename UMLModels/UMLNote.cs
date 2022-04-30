namespace UMLModels
{
    public class UMLNote : UMLDataType
    {
        public UMLNote(string text, string? alias) : base("UMLNote")
        {
            Text = text;
            Alias = alias;
        }

        public string Text
        {
            get; set;
        }
        public string? Alias
        {
            get;
            set;
        }
    }
}