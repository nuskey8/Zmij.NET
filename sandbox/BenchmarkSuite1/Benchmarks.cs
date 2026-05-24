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

    static readonly float[] TestValuesF = new float[10000];

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
    public void System_FloatToString()
    {
        foreach (var value in TestValuesF)
        {
            _ = value.ToString();
        }
    }

    [Benchmark]
    public void System_Double_Utf8Formatter()
    {
        foreach (var value in TestValues)
        {
            Utf8Formatter.TryFormat(value, buffer.AsSpan(), out var _);
        }
    }

    [Benchmark]
    public void System_Float_Utf8Formatter()
    {
        foreach (var value in TestValuesF)
        {
            Utf8Formatter.TryFormat(value, buffer.AsSpan(), out var _);
        }
    }

    [Benchmark]
    public void Zmij_Double_ToString()
    {
        foreach (var value in TestValues)
        {
            _ = Zmij.ToString(value);
        }
    }

    [Benchmark]
    public void Zmij_Float_ToString()
    {
        foreach (var value in TestValuesF)
        {
            _ = Zmij.ToString(value);
        }
    }

    [Benchmark]
    public void Zmij_Double_TryWrite()
    {
        foreach (var value in TestValues)
        {
            Zmij.TryWrite(value, buffer.AsSpan(), out var _);
        }
    }

    [Benchmark]
    public void Zmij_Float_TryWrite()
    {
        foreach (var value in TestValuesF)
        {
            Zmij.TryWrite(value, buffer.AsSpan(), out var _);
        }
    }
}
