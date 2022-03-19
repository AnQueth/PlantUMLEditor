using System;
using System.Windows;

namespace PlantUMLEditor.Models
{
    public class GridSettings
    {

        private GridLength _DataTypesHeight = new(200);
        private GridLength _DocumentsWell = new(200);
        private GridLength _TreeWidth = new(200);
        private Action? _changedCB;

        public GridLength TreeWidth
        {
            get => _TreeWidth;
            set
            {
                _TreeWidth = value;
                _changedCB?.Invoke();
            }
        }

        public GridLength DocumentsWell
        {
            get => _DocumentsWell;
            set
            {
                _DocumentsWell = value;
                _changedCB?.Invoke();
            }
        }

        public GridLength DataTypesHeight
        {
            get => _DataTypesHeight;
            set
            {
                _DataTypesHeight = value;
                _changedCB?.Invoke();
            }
        }

        internal Action ChangedCB
        {
            set => _changedCB = value;
        }
    }
}
