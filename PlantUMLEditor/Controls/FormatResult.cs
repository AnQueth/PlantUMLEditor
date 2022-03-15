using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{

    public record FormatResult(Brush Brush, int Start, int Length, FontWeight FontWeight, string Match)
    {
        public int Span => Start + Length;

        public static bool Intersects(int begin, int end, int start, int span)
        {
            if (start <= begin && span >= begin)
            {
                return true;
            }

            if (start <= end && span >= end)
            {
                return true;
            }

            if (start >= begin && span <= end)
            {
                return true;
            }

            return false;
        }

        public bool Intersects(int begin, int end)
        {
            return Intersects(begin, end, Start, Span);
        }

        public static int AdjustedStart(int begin, int start)
        {
            return Math.Max(0, start - begin);
        }

        public int AdjustedStart(int begin)
        {

            return AdjustedStart(begin, Start);



        }

        public static int AdjustedLength(int begin, int end, int start, int length, int span)
        {
            int len = length;
            int startShave = 0;
            if (start - begin < 0)
            {
                startShave = start - begin;
                Debug.WriteLine($"SS {startShave}");
                len = length + startShave;
            }

            //500 650
            //500 200
            if (span > end) // 700 > 650
            {
                len = length - (span - end) + startShave; //700 - 650 = 50, 200 - 50 = 150 length
            }

            return Math.Min(len, end - begin);
        }

        public int AdjustedLength(int begin, int end)
        {
            return AdjustedLength(begin, end, Start, Length, Span);
        }
    }

}