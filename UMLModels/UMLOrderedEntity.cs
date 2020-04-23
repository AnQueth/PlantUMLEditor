namespace UMLModels
{
    public abstract class UMLOrderedEntity
    {
       public virtual  string Warning { get
            {
                return null;
            }
        }
        public int LineNumber { get; set; }
    }
}