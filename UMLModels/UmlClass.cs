namespace UMLModels
{
    public class UMLClass : UMLDataType
    {
        public UMLClass(string @namespace, bool isAbstract, 
            string name, UMLClass baseClass = null, params UMLInterface[] interfaces) : base(name, @namespace, interfaces)
        {
            this.Base = baseClass;
            this.IsAbstract = isAbstract;
        }

        public bool IsAbstract { get;   set; }


    }
}