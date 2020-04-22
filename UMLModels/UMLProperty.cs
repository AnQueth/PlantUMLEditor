﻿namespace UMLModels
{
    public class UMLProperty : UMLParameter
    {
        public UMLProperty()
        {
        }

        public UMLProperty(string name, UMLDataType type, UMLVisibility visibility, ListTypes listType) : base(name, type, listType)
        {
            Visibility = visibility;
        }
        public UMLVisibility Visibility
        {
            get; set;
        }

        public bool IsReadOnly { get; set; }
    }
}