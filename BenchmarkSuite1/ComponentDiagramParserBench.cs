using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using PlantUML;
using Microsoft.VSDiagnostics;

namespace BenchmarkSuite1
{
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [CPUUsageDiagnoser]
    public class ComponentDiagramParserBench
    {
        private string _largeDiagram;
        [GlobalSetup]
        public void Setup()
        {
            // pre-size to reduce allocations
            var sb = new StringBuilder(16384);
            sb.AppendLine("@startuml");
            for (int i = 0; i < 200; i++)
            {
                sb.AppendLine($"component C{i} as C{i} #FFAA00");
            }

            for (int i = 0; i < 180; i++)
            {
                sb.AppendLine($"C{i} --> C{i + 1}");
            }

            for (int i = 200; i < 300; i++)
            {
                sb.AppendLine($"[Component {i}] as Comp{i}");
            }

            for (int i = 210; i < 295; i++)
            {
                sb.AppendLine($"Comp{i} o-- Comp{i + 1}");
            }

            sb.AppendLine("@enduml");
            _largeDiagram = sb.ToString();
        }

        [Benchmark]
        public void ParseLargeString_Run()
        {
            // follow repository pattern: synchronous benchmark that blocks on async parser
            var res = UMLComponentDiagramParser.ReadString(_largeDiagram, componentsMustBeDefined: false).GetAwaiter().GetResult();
            // prevent optimizer from eliding the call
            if (res == null)
                Console.WriteLine("");
        }
    }
}