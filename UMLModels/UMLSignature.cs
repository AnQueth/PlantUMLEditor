﻿namespace UMLModels
{
    public class UMLSignature
    {
       
        public string Signature
        {
            get
            {
                return this.ToString() ?? string.Empty;
            }
        }
    }
}