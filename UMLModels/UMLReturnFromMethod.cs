namespace UMLModels
{
    public class UMLReturnFromMethod : UMLMethod
    {
        public UMLReturnFromMethod()
        {
        }

        public UMLReturnFromMethod(UMLMethod returningFrom) : base(returningFrom.ReturnType, UMLVisibility.Public)
        {
        }
    }
}