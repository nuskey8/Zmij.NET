using System;
using System.Buffers.Text;
using BenchmarkDotNet.Attributes;
using NRandom;
using ZmijNet;

namespace BenchmarkSuite1;

public class Benchmarks
{
    static readonly double[] TestValues = new double[10000];
    static readonly byte[] buffer = new byte[1000];

    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < TestValues.Length; i++)
        {
            TestValues[i] = RandomEx.Shared.NextDouble(double.MinValue, double.MaxValue);
        }
    }

    [Benchmark]
    public void System_DoubleToString()
    {
        foreach (var value in TestValues)
        {
            _ = value.ToString();
        }
    }

    [Benchmark]
    public void System_Utf8Formatter()
    {
        foreach (var value in TestValues)
        {
            Utf8Formatter.TryFormat(value, buffer.AsSpan(), out var _);
        }
    }

    [Benchmark]
    public void Zmij_ToString()
    {
        foreach (var value in TestValues)
        {
            _ = Zmij.ToString(value);
        }
    }

    [Benchmark]
    public void Zmij_TryWrite()
    {
        foreach (var value in TestValues)
        {
            Zmij.TryWrite(value, buffer.AsSpan(), out var _);
        }
    }
}
