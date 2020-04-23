namespace UMLModels
{
    public class UMLSequenceConnection : UMLOrderedEntity
    {
        public UMLSequenceLifeline From { get; set; }
        public UMLSequenceLifeline To { get; set; }

        public UMLMethod Action { get; set; }

        public override string Warning
        {
            get
            {
                if(Action is UMLUnknownAction)
                    return $"Action {Action.Signature} is unknown for {From?.Text} to {To?.Text}";
                return null;
            }
        }
    }
}