using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using PlantUML;
using Microsoft.VSDiagnostics;

namespace BenchmarkSuite1
{
    [CPUUsageDiagnoser]
    public class UMLClassDiagramParserBench
    {
        private string sample;
        [GlobalSetup]
        public void Setup()
        {
            var estimatedCharsPerClass = 64;
            var count = 1000;
            var sb = new StringBuilder(count * estimatedCharsPerClass);
            sb.AppendLine("@startuml");
            for (int i = 0; i < count; i++)
            {
                sb.AppendLine($"class Class{i} {{");
                sb.AppendLine($"  + int GetId{i}() : int");
                sb.AppendLine($"  + string Name{i}");
                sb.AppendLine("}");
            }

            sb.AppendLine("@enduml");
            sample = sb.ToString();
        }

        [Benchmark]
        public void Parse_Run()
        {
            object results = PlantUML.UMLClassDiagramParser.ReadString(sample).GetAwaiter().GetResult();
            if (results == null)
                Console.WriteLine("");
        }
    }
}