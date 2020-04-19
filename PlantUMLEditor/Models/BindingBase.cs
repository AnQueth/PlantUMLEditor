using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace PlantUMLEditor.Models
{
    public abstract class BindingBase : INotifyPropertyChanged
    {
        protected void SetValue<T>(ref T variable, T v, [CallerMemberName] string name = null)
        {
            variable = v;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
