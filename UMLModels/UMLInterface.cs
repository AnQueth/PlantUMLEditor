﻿using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
   public class UMLInterface : UMLDataType
    {
        public UMLInterface()
        {

        }
        public UMLInterface(string @namespace, string name, UMLInterface @base = null) : base( name, @namespace)
        {
            Base = @base;
        }

    }
}
