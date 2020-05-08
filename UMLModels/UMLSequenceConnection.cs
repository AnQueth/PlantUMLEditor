namespace UMLModels
{
    public class UMLSequenceConnection : UMLOrderedEntity
    {
        public UMLSignature Action { get; set; }
        public UMLSequenceLifeline From { get; set; }
        public string FromName { get; set; }
        public bool FromShouldBeUsed { get; set; }
        public UMLSequenceLifeline To { get; set; }

        public string ToName { get; set; }
        public bool ToShouldBeUsed { get; set; }

        public override string Warning
        {
            get
            {
                if (Action is UMLUnknownAction && (To == null || !To.FreeFormed))
                    return $"Action {Action.Signature} is unknown for {From?.Text} to {To?.Text}";
                if (From is null && To is null)
                    return $"From {FromName} To {ToName} is not defined in participants";
                if (To is null && ToShouldBeUsed)
                    return $"From {From.Alias} To {ToName} is not defined in participants";
                if (From is null && FromShouldBeUsed)
                    return $"From {FromName} To {To.Alias} is not defined in participants";
                return null;
            }
        }
    }
}