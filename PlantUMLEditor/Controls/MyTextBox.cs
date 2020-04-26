using PlantUMLEditor.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PlantUMLEditor.Controls
{
    public class MyTextBox : TextBox, INotifyPropertyChanged
    {

        public static readonly DependencyProperty BindedDocumentProperty =
       DependencyProperty.Register(
       nameof(BindedDocument), typeof(DocumentModel),
       typeof(MyTextBox), new PropertyMetadata(BindedDocumentPropertyChanged)
       );

        private static void BindedDocumentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var m = d as MyTextBox;
            var value = (DocumentModel) e.NewValue;

            if (value != null)
            {
                value.TextWrite = m.SetText;
                value.TextRead = m.ReadText;
                value.InsertText = m.InsertText;
                value.Binded();
            }

            value = (DocumentModel)e.OldValue;
            if (value != null)
            {
                value.TextWrite = null;
                value.TextRead = null;
                value.InsertText = null;
            }

        }

        private FixedDocument styledDocument = new FixedDocument();

        public event PropertyChangedEventHandler PropertyChanged;

        public FixedDocument StyledDocument
        {
            get { return styledDocument; }
            set
            {
                styledDocument = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StyledDocument)));

            }
        }
        public DocumentModel BindedDocument
        {
            get
            {
                return (DocumentModel) GetValue(BindedDocumentProperty);
            }
            set
            {
                SetValue(BindedDocumentProperty, value);
         
            }
        }

        public void InsertText(string text)
        {

    
           

            Text = Text.Insert(CaretIndex, text);

 

        }

        private void SetText(string text )
        {


            this.Text = text;
        }

        private string ReadText()
        {
             
            return this.Text;
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            
            BindedDocument.TextChanged(ReadText());
            base.OnTextChanged(e);

            SynatxFlowDocument syntaxFlowDocument = new SynatxFlowDocument();
            syntaxFlowDocument.SetText(this.Text);
            StyledDocument = syntaxFlowDocument.Document;

        }


        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
          

           


    

           

            var rec = GetRectFromCharacterIndex(CaretIndex);

            var line = GetLineIndexFromCharacterIndex(CaretIndex);
            string text = GetLineText(line).Trim();

            BindedDocument.AutoComplete(rec, text, line);
            base.OnPreviewKeyDown(e);


        }

       
    }
}
