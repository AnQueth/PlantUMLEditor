namespace UMLModels
{
    public class UMLProperty : UMLParameter
    {


        public UMLProperty(string name, UMLDataType type, UMLVisibility visibility, ListTypes listType, bool isStatic, bool isAbstract) : base(name, type, listType)
        {
            Visibility = visibility;
            IsStatic = isStatic;
            IsAbstract = isAbstract;
        }

        public bool IsAbstract
        {
            get; set;

        }
        public bool IsStatic
        {
            get; set;
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