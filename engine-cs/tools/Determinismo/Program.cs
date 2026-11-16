// The rules the same to the bit on another runtime (Probe.cs): with .NET, `dotnet run --project engine-cs/tools/Determinismo [seeds] [folder for the final states]`.
using System;
using System.Diagnostics;
using Bastion.Tools;

var seeds = args.Length > 0 ? int.Parse(args[0]) : 4;
var clock = Stopwatch.StartNew();
var dump = args.Length > 1 ? args[1] : null;
foreach (var line in DeterminismProbe.Run(seeds, dump)) Console.WriteLine($"[determinismo] {line}");
Console.WriteLine($"[determinismo] hecho en {clock.Elapsed.TotalSeconds:F1} s con .NET {Environment.Version}");
