namespace UMLModels
{
    public class UMLNoteConnection
    {
        public string First
        {
            get; set;

        }
        public string Connector
        {
            get; set;

        }
        public string Second
        {
            get; set;

        }

        public UMLNoteConnection(string first, string connector, string second)
        {
            First = first;
            Connector = connector;
            Second = second;
        }
    }
}
