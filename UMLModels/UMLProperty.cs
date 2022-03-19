namespace UMLModels
{
    public class UMLProperty : UMLParameter
    {


        public UMLProperty(string name, UMLDataType type, UMLVisibility visibility, ListTypes listType) : base(name, type, listType)
        {
            Visibility = visibility;
        }

        public bool IsReadOnly
        {
            get; set;
        }

        public UMLVisibility Visibility
        {
            get; set;
        }
    }
}