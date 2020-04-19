using System;
using System.Collections.Generic;
using System.Text;

namespace UMLModels
{
    public class UMLSequenceLifeline
    {

        public string DataTypeId
        {
            get; set;
        }
        public string Alias { get; set; }



        public string Text { get; set; }

        public int LineNumber { get; set; }




        public UMLSequenceLifeline(string name, string alias, string dataTypeId)
        {
            Text = name;
            DataTypeId = dataTypeId;
            Alias = alias;
        }


    }
}
