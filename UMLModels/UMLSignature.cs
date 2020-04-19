using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public abstract class UMLSignature
    {
        public string Signature
        {
            get
            {
                return ToString();
            }
        }
    }
}
