﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Compression;

namespace UMLModels
{
    public class UMLClassDiagram : UMLDiagram
    {
        public UMLClassDiagram()
        {
        }

        public UMLClassDiagram(string title, string fileName)
        {
            Title = title;

            FileName = fileName;
        }

        public List<UMLDataType> DataTypes
        {
            get
            {
                List<UMLDataType> dt = new List<UMLDataType>();

                AddMore(Package, dt);

                return dt;
            }
        }

        public UMLPackage Package { get; set; }

        private void AddMore(UMLPackage p, List<UMLDataType> dt)
        {
            foreach (var c in p.Children)
            {
                if (c is UMLPackage z)
                {
                    AddMore(z, dt);
                }
                else if (!(c is UMLNote))
                {
                    dt.Add(c);
                }
            }
        }
    }
}