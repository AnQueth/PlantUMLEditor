namespace UMLModels
{
    public class UMLError
    {
        public UMLError(string text, string value, int lineNumber)
        {
            Value = text + " " + value;
            LineNumber = lineNumber;
        }

        public int LineNumber
        {
            get;
        }
        public string Value
        {
            get;
        }
    }
}