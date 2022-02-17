namespace UMLModels
{
    public class UMLSequenceOther : UMLOrderedEntity
    {
        public UMLSequenceOther(int lineNumber, string text) : base(lineNumber)
        {
            Text = text;
        }

        public string Text
        {
            get;
            set;
        }
    }
}
