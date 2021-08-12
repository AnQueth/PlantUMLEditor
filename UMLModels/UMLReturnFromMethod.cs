namespace UMLModels
{
    public class UMLReturnFromMethod : UMLSignature
    {
        private UMLSignature _returningFrom;

        
        public UMLReturnFromMethod(UMLSignature returningFrom)
        {
            _returningFrom = returningFrom;
        }

        public UMLSignature ReturningFrom
        {
            get
            {
                return _returningFrom;
            }
            set
            {
                _returningFrom = value;
            }
        }

        public override string ToString()
        {
            return ReturningFrom.Signature;
        }
    }
}