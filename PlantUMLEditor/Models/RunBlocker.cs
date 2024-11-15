using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    class RunBlocker
    {
        private TaskCompletionSource? _continuer;


        public void Block()
        {
            _continuer = new TaskCompletionSource();
           
        }

        public async Task Wait()
        {
            await _continuer!.Task; 
        }

        internal void Unblock()
        {
           _continuer!.SetResult();
        }
    }
}
