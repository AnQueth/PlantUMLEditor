namespace UMLModels
{
    public abstract class UMLOrderedEntity
    {
        public int LineNumber { get; set; }
        public virtual string Warning { get; }
    }
}