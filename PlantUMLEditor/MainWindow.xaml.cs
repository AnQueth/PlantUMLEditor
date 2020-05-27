﻿using PlantUMLEditor.Models;
using PlantUMLEditor.Services;
using System;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Input;
using System.Windows.Media;

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
            _model = new MainModel(new OpenDirectoryService(), new UMLDocumentCollectionSerialization());
            DataContext = _model;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
        }
    }
}