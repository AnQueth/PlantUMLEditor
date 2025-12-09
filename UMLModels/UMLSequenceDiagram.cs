using System;
using System.Collections.Generic;

namespace UMLModels
{
    public class UMLSequenceDiagram : UMLDiagram
    {


        public UMLSequenceDiagram(string title, string fileName) : base(title, fileName)
        {


            ValidateAgainstClasses = true;
        }

        public List<UMLOrderedEntity> FlattenedEntities 
        { 
            get
            {
                var list = new List<UMLOrderedEntity>();
                
               
                foreach (var entity in Entities)
                {
                    AddRecursive(entity, list);
                   
                }

                return list;
            }
        }

        private void AddRecursive(UMLOrderedEntity entity, List<UMLOrderedEntity> list)
        {
            list.Add(entity);
            if(entity is UMLSequenceBlockSection blockSection)
            {
                foreach(var child in blockSection.Entities)
                {
                    AddRecursive(child, list);
                }
            }
        
        }

        public List<UMLOrderedEntity> Entities { get; init; } = new();
        public List<UMLSequenceLifeline> LifeLines { get; init; } = new();

        public bool LaxMode
        {
                       get; set;

        }

        public bool ValidateAgainstClasses
        {
            get; set;
        }

      
    }
}