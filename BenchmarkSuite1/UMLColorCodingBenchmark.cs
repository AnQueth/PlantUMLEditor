using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PlantUMLEditor.Controls;
using Microsoft.VSDiagnostics;

namespace PlantUMLEditor.Benchmarks
{
    [CPUUsageDiagnoser]
    public class UMLColorCodingBenchmark
    {
        private UMLColorCoding _coder;
        private string _sampleText;
        [GlobalSetup]
        public void Setup()
        {
            _coder = new UMLColorCoding();
            // A representative PlantUML document exercising many regexes
            _sampleText = @"@startuml
title Sample Diagram
actor Alice
participant Bob as B
Alice -> Bob: Hello
note right of Bob: This is a note
' a comment line
class MyClass {
  +publicMethod()
}
@enduml";
        }

        [Benchmark]
        public void FormatText_Run()
        {
            var results = _coder.FormatText(_sampleText);
            // prevent optimizations
            if (results == null || results.Count < 0)
                System.Console.WriteLine("");
        }
    }
}