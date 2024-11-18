using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlantUMLEditor.Models
{
    public abstract class BindingBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void SetValue<T>(ref T variable, T v, [CallerMemberName] string? name = null)
        {
            variable = v;
            PropertyChangedInvoke(name);
        }

        protected void PropertyChangedInvoke([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}