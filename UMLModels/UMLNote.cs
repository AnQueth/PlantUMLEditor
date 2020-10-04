namespace UMLModels
{
    public class UMLNote : UMLDataType
    {
        public UMLNote(string text) : base("UMLNote")
        {
            Text = text;
        }

        public string Text
        {
            get; set;
        }
    }
}