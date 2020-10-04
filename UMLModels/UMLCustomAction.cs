namespace UMLModels
{
    public class UMLCustomAction : UMLMethod
    {
        public UMLCustomAction()
        {
        }

        public UMLCustomAction(string name) : base(name, new VoidDataType(), UMLVisibility.Public)
        {
        }

        public override string ToString()
        {
            return Name;
        }
    }
}