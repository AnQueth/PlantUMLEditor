﻿using PlantUMLEditor.Models;
using PlantUMLEditor.Services;
using System;
using System.Windows;

namespace PlantUMLEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainModel _model;

        private bool setEventHandler = false;

        public MainWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            InitializeComponent();
            _model = new MainModel(new IOService(), new UMLDocumentCollectionSerialization());
            DataContext = _model;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ((MainModel)DataContext).LoadedUI();
        }
    }
}