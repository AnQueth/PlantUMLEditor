namespace UMLModels
{
    public abstract class UMLOrderedEntity
    {
        public UMLOrderedEntity(int lineNumber)
        {
            LineNumber = lineNumber;
            
        }

        public int LineNumber { get; init; }
        public virtual string? Warning { get; }
    }
}