namespace UMLModels
{
    public class UMLProperty : UMLParameter
    {


        public UMLProperty(string name, UMLDataType type, UMLVisibility visibility,
            ListTypes listType, bool isStatic, bool isAbstract, bool drawnWithLine) : base(name, type, listType)
        {
            Visibility = visibility;
            IsStatic = isStatic;
            IsAbstract = isAbstract;
            DrawnWithLine = drawnWithLine;
        }

        public bool IsAbstract
        {
            get; set;

        }
        public bool DrawnWithLine
        {
            get;
            private set;
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