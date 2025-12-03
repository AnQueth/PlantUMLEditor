using System;
using System.Linq;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
        public static class FlowDocumentCompactor
        {
            public static void CompactFlowDocument(System.Windows.Documents.FlowDocument doc)
            {
                doc.PagePadding = new System.Windows.Thickness(6);
                doc.ColumnWidth = double.PositiveInfinity; // avoid column wrapping
                foreach (var block in doc.Blocks.ToList())
                    CompactBlock(block);
            }

            public static void CompactBlock(System.Windows.Documents.Block block)
            {
                switch (block)
                {
                    case System.Windows.Documents.Paragraph p:
                        p.Margin = new System.Windows.Thickness(0, 0, 0, 4);
                        p.LineStackingStrategy = System.Windows.LineStackingStrategy.MaxHeight;
                        p.LineHeight = p.FontSize * 1.15;
                        break;

                    case System.Windows.Documents.List list:
                        list.Margin = new System.Windows.Thickness(0, 0, 0, 4);
                        list.MarkerOffset = 12;
                        foreach (var li in list.ListItems)
                        {
                            li.Margin = new System.Windows.Thickness(0, 0, 0, 2);
                            foreach (var inner in li.Blocks.ToList()) CompactBlock(inner);
                        }
                        break;

                    case System.Windows.Documents.Section s:
                        s.Margin = new System.Windows.Thickness(0);
                        foreach (var inner in s.Blocks.ToList()) CompactBlock(inner);
                        break;

                    default:
                        // handle other block types if needed
                        break;
                }
            }
        }
    }
}