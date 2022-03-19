namespace UMLModels
{
    public class UMLSequenceConnection : UMLOrderedEntity
    {
        public UMLSequenceConnection(UMLSequenceLifeline from, UMLSequenceLifeline to, int lineNumber) : base(lineNumber)
        {
            From = from;
            To = to;
            ToName = string.Empty;
            FromName = string.Empty;
        }
        public UMLSequenceConnection(UMLSequenceLifeline from, UMLSignature action, int lineNumber) : base(lineNumber)
        {
            From = from;
            Action = action;
            ToName = string.Empty;
            FromName = string.Empty;
        }

        public UMLSequenceConnection(UMLSequenceLifeline? to, bool toShouldBeUsed, UMLSignature? action, string toAlias, int lineNumber) : base(lineNumber)
        {
            From = null;
            FromName = string.Empty;
            To = to;
            ToShouldBeUsed = toShouldBeUsed;
            Action = action;
            ToName = toAlias;
        }
        public UMLSequenceConnection(UMLSequenceLifeline? from, UMLSequenceLifeline? to, UMLSignature? action,
            string fromAlias, string toAlias, bool fromShouldBeUsed, bool toShouldBeUsed, int lineNumber) : base(lineNumber)
        {
            From = from;
            FromName = fromAlias;
            To = to;
            FromShouldBeUsed = fromShouldBeUsed;
            ToShouldBeUsed = toShouldBeUsed;
            Action = action;
            ToName = toAlias;
        }
        public UMLSignature? Action
        {
            get; set;
        }
        public UMLSequenceLifeline? From
        {
            get; set;
        }
        public string FromName
        {
            get; set;
        }
        public bool FromShouldBeUsed
        {
            get; set;
        }
        public UMLSequenceLifeline? To
        {
            get; set;
        }

        public string ToName
        {
            get; set;
        }
        public bool ToShouldBeUsed
        {
            get; set;
        }

        public override string? Warning
        {
            get
            {
                if (Action is UMLUnknownAction && (To == null || !To.FreeFormed))
                {
                    return $"Action {Action.Signature} is unknown for {From?.Text} to {To?.Text}";
                }

                if (From is null && To is null)
                {
                    return $"From {FromName} To {ToName} is not defined in participants";
                }

                if (To is null && ToShouldBeUsed && From is not null)
                {
                    return $"From {From?.Alias} To {ToName} is not defined in participants";
                }

                if (From is null && FromShouldBeUsed && To is not null)
                {
                    return $"From {FromName} To {To.Alias} is not defined in participants";
                }

                return null;
            }
        }
    }
}