namespace UMLModels
{
    public class UMLUnknownAction : UMLMethod
    {


        public UMLUnknownAction(string text) : base(text, new VoidDataType(), UMLVisibility.Public)
        {
        }

        public override string ToString()
        {
            return Name;
        }
    }
}