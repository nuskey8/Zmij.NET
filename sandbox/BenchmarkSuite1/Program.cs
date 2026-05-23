using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using BenchmarkSuite1;

var config = DefaultConfig.Instance;
BenchmarkRunner.Run<Benchmarks>(config, args);
