namespace UMLModels
{
    public class UMLComment : UMLDataType
    {
        public UMLComment(string text) : base("UMLComment")
        {
            Text = text;
        }

        public string Text
        {
            get; set;
        }
    }
}