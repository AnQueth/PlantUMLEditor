using System.Collections.Generic;

namespace UMLModels
{
    public class UMLComponent : UMLDataType
    {
        public UMLComponent(string @namespace, string name, string alias) : base(name, @namespace, alias)
        {
            Exposes = new List<UMLDataType>();
            Consumes = new List<UMLDataType>();

        }

        public List<UMLDataType> Children
        {
            get; set;
        } = new List<UMLDataType>();

        public List<UMLDataType> Consumes
        {
            get; set;
        }
        public List<UMLDataType> Exposes
        {
            get; set;
        }

        public List<string> PortsIn
        {
            get; set;
        } = new List<string>();

        public List<string> PortsOut
        {
            get; set;
        } = new List<string>();

        public List<string> Ports
        {
            get; set;
        } = new List<string>();
    }
}