namespace UMLModels
{
    public class UMLSequenceConnection : UMLOrderedEntity
    {
        public UMLSequenceLifeline From { get; set; }
        public UMLSequenceLifeline To { get; set; }

        public UMLSignature Action { get; set; }

        public bool ToShouldBeUsed { get; set; }

        public override string Warning
        {
            get
            {
                if(Action is UMLUnknownAction && ( To == null ||  !To.FreeFormed))
                    return $"Action {Action.Signature} is unknown for {From?.Text} to {To?.Text}";
                if (To is null && ToShouldBeUsed)
                    return "To is not a valid lifeline";

                return null;
            }
        }
    }
}