﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace PlantUMLEditor.Models
{
    public class GridSettings
    {
        public GridSettings(Action changedCb)
        {
            _changedCB = changedCb;

        }
        private GridLength _DataTypesHeight = new GridLength(200);
        private GridLength _DocumentsWell = new GridLength(200);
        private GridLength _TreeWidth = new GridLength(200);
        private   Action _changedCB;

        public GridLength TreeWidth
        {
            get
            {
                return _TreeWidth;
            }
            set
            {
                _TreeWidth = value;
                _changedCB?.Invoke();
            }
        }

        public GridLength DocumentsWell
        {
            get
            {
                return _DocumentsWell;
            }
            set
            {
                _DocumentsWell = value;
                _changedCB?.Invoke();
            }
        }

        public GridLength DataTypesHeight
        {
            get
            {
                return _DataTypesHeight;
            }
            set
            {
                _DataTypesHeight = value;
                _changedCB?.Invoke();
            }
        }

        internal Action ChangedCB
        {
              set
            {
                _changedCB = value;

            }
        }
    }
}
