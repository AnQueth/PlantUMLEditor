namespace UMLModels
{
    public class UMLCreateAction : UMLMethod
    {
       

        public UMLCreateAction(string name) : base(name, new VoidDataType(), UMLVisibility.Public)
        {
        }

        public override string ToString()
        {
            return Name;
        }
    }
}