﻿using PlantUMLEditor.Models;
using PlantUMLEditor.Services;
using System;
using System.ComponentModel;
using System.Windows;
using System.Threading.Tasks;

namespace PlantUMLEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainModel _model;

     

        public MainWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            InitializeComponent();
            _model = new MainModel(new IOService(), new UMLDocumentCollectionSerialization());
            DataContext = _model;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_model.CloseAll())
                e.Cancel = true;

            base.OnClosing(e);
        }
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(50);

            ((MainModel)DataContext).LoadedUI();
        }
    }
}