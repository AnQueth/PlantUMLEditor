namespace UMLModels
{
    public class UMLLifelineReturnAction : UMLMethod
    {
        public UMLLifelineReturnAction(string text) : base(text, new VoidDataType(), UMLVisibility.Public)
        {
        }

        public override string ToString()
        {
            return Name;
        }
    }
}