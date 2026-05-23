using System.Globalization;
using System.Text;

namespace ZmijNet.Tests;

public class ZmijTests
{
    [Test]
    public async Task Zero_ToString_ReturnsZero()
    {
        await Assert.That(Zmij.ToString(0.0)).IsEqualTo("0");
    }

    [Test]
    public async Task NegativeZero_ToString_ReturnsNegativeZero()
    {
        await Assert.That(Zmij.ToString(-0.0)).IsEqualTo("-0");
    }

    [Test]
    public async Task One_ToString_ReturnsOne()
    {
        await Assert.That(Zmij.ToString(1.0)).IsEqualTo("1");
    }

    [Test]
    public async Task NegativeOne_ToString_ReturnsMinusOne()
    {
        await Assert.That(Zmij.ToString(-1.0)).IsEqualTo("-1");
    }

    [Test]
    [Arguments(0.0, "0")]
    [Arguments(1.0, "1")]
    [Arguments(-1.0, "-1")]
    [Arguments(3.14, "3.14")]
    [Arguments(-3.14, "-3.14")]
    [Arguments(100.0, "100")]
    [Arguments(0.5, "0.5")]
    [Arguments(1.5, "1.5")]
    [Arguments(double.PositiveInfinity, "inf")]
    [Arguments(double.NegativeInfinity, "-inf")]
    [Arguments(double.Epsilon, "5e-324")]
    [Arguments(double.MaxValue, "1.7976931348623157e+308")]
    [Arguments(double.MinValue, "-1.7976931348623157e+308")]
    public async Task ToString_KnownValues(double value, string expected)
    {
        await Assert.That(Zmij.ToString(value)).IsEqualTo(expected);
    }

    [Test]
    public async Task ToString_Nan_OutputIsNan()
    {
        string result = Zmij.ToString(double.NaN);
        await Assert.That(result.ToLowerInvariant()).IsEqualTo("-nan");
    }

    [Test]
    public async Task ToString_RoundtripsThroughDoubleParse()
    {
        var random = new Random(42);
        var values = new List<double>
        {
            0.0,
            -0.0,
            1.0,
            -1.0,
            3.14,
            -3.14,
            0.1,
            0.2,
            0.3,
            0.7,
            0.99,
            1.0 / 3.0,
            1.0 / 7.0,
            Math.PI,
            Math.E,
            1e10,
            1e-10,
            1e20,
            1e-20,
            1e100,
            1e-100,
            1e300,
            1e-300,
            123.456,
            -987.654e50,
            0.00123456,
            100000000000000.0,
            0.0000000000001,
            double.Epsilon,
            double.MaxValue,
            double.MinValue,
        };

        for (int i = 0; i < 500; i++)
        {
            double d = (random.NextDouble() - 0.5) * 1e10;
            values.Add(d);
            values.Add(d * 1e-10);
            values.Add(d * 1e100);
            values.Add(-d);
        }

        foreach (var value in values)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                continue;
            }

            string str = Zmij.ToString(value);
            bool parsed = double.TryParse(
                str,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double roundtrip
            );
            await Assert.That(parsed).IsTrue();

            if (value == 0.0)
            {
                await Assert.That(roundtrip).IsEqualTo(0.0);
                continue;
            }

            if (double.IsInfinity(value))
            {
                await Assert.That(double.IsInfinity(roundtrip)).IsTrue();
                continue;
            }

            double diff = Math.Abs(roundtrip - value) / Math.Max(1.0, Math.Abs(value));
            await Assert.That(diff).IsLessThanOrEqualTo(1e-15);
        }
    }

    [Test]
    public async Task TryWrite_Zero_WritesZero()
    {
        byte[] buffer = new byte[64];
        bool success = Zmij.TryWrite(0.0, buffer, out int bytesWritten);
        await Assert.That(success).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(buffer[..bytesWritten])).IsEqualTo("0");
    }

    [Test]
    public async Task TryWrite_NegativeZero_WritesNegativeZero()
    {
        byte[] buffer = new byte[64];
        bool success = Zmij.TryWrite(-0.0, buffer, out int bytesWritten);
        await Assert.That(success).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(buffer[..bytesWritten])).IsEqualTo("-0");
    }

    [Test]
    [Arguments(0.0, "0")]
    [Arguments(1.0, "1")]
    [Arguments(-1.0, "-1")]
    [Arguments(3.14, "3.14")]
    [Arguments(double.PositiveInfinity, "inf")]
    [Arguments(double.NegativeInfinity, "-inf")]
    [Arguments(double.MaxValue, "1.7976931348623157e+308")]
    [Arguments(double.MinValue, "-1.7976931348623157e+308")]
    public async Task TryWrite_KnownValues(double value, string expectedString)
    {
        byte[] buffer = new byte[64];
        bool success = Zmij.TryWrite(value, buffer, out int bytesWritten);
        await Assert.That(success).IsTrue();
        await Assert
            .That(Encoding.UTF8.GetString(buffer[..bytesWritten]))
            .IsEqualTo(expectedString);
    }

    [Test]
    public async Task TryWrite_Nan_WritesNan()
    {
        byte[] buffer = new byte[64];
        Zmij.TryWrite(double.NaN, buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer[..bytesWritten]);
        await Assert.That(result.ToLowerInvariant()).IsEqualTo("-nan");
    }

    [Test]
    public async Task TryWrite_BufferTooSmall_ReturnsFalse()
    {
        byte[] tiny = new byte[1];
        bool success = Zmij.TryWrite(123.456, tiny, out int bytesWritten);
        await Assert.That(success).IsFalse();
        await Assert.That(bytesWritten).IsEqualTo(0);
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(1.0)]
    [Arguments(-1.0)]
    [Arguments(double.MaxValue)]
    [Arguments(double.MinValue)]
    public async Task TryWrite_ExactBufferSize_Succeeds(double value)
    {
        string expected = Zmij.ToString(value);
        int exactSize = Encoding.UTF8.GetByteCount(expected);
        byte[] buffer = new byte[exactSize];
        bool success = Zmij.TryWrite(value, buffer, out int bytesWritten);
        await Assert.That(success).IsTrue();
        await Assert.That(bytesWritten).IsEqualTo(exactSize);
        await Assert.That(Encoding.UTF8.GetString(buffer[..bytesWritten])).IsEqualTo(expected);
    }

    [Test]
    public async Task TryWrite_RoundtripsThroughDoubleParse()
    {
        var random = new Random(42);
        byte[] buffer = new byte[64];

        for (int i = 0; i < 200; i++)
        {
            double value = (random.NextDouble() - 0.5) * 1e10;
            bool success = Zmij.TryWrite(value, buffer, out int bytesWritten);
            await Assert.That(success).IsTrue();
            string str = Encoding.UTF8.GetString(buffer[..bytesWritten]);
            double parsed = double.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture);
            double diff = Math.Abs(parsed - value) / Math.Max(1.0, Math.Abs(value));
            await Assert.That(diff).IsLessThanOrEqualTo(1e-15);
        }
    }

    [Test]
    [Arguments(double.NaN)]
    [Arguments(double.PositiveInfinity)]
    [Arguments(double.NegativeInfinity)]
    public async Task ToDecimal_NonFinite_ReturnsNonFiniteExponent(double value)
    {
        ZmijDecimal dec = Zmij.ToDecimal(value);
        await Assert.That(dec.Exponent).IsEqualTo(int.MaxValue);
    }

    [Test]
    [Arguments(0.0, false)]
    [Arguments(1.0, false)]
    [Arguments(-1.0, true)]
    [Arguments(double.NegativeInfinity, true)]
    [Arguments(double.PositiveInfinity, false)]
    public async Task ToDecimal_IsNegative(double value, bool expected)
    {
        ZmijDecimal dec = Zmij.ToDecimal(value);
        await Assert.That(dec.IsNegative).IsEqualTo(expected);
    }

    [Test]
    public async Task ToString_PowersOfTenRoundtrip()
    {
        for (int exp = -50; exp <= 50; exp++)
        {
            double value = double.Parse($"1e{exp}", CultureInfo.InvariantCulture);
            string str = Zmij.ToString(value);
            double parsed = double.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture);
            await Assert.That(parsed).IsEqualTo(value);
        }
    }

    [Test]
    public async Task ToString_SubnormalNumbersRoundtrip()
    {
        var random = new Random(99);
        for (int i = 0; i < 100; i++)
        {
            ulong bits = ((ulong)(uint)random.Next() << 32) | (ulong)(uint)random.Next();
            bits &= (1UL << 52) - 1;
            double value = BitConverter.Int64BitsToDouble((long)bits);
            if (double.IsNaN(value))
                continue;
            string str = Zmij.ToString(value);
            double parsed = double.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture);
            await Assert.That(parsed).IsEqualTo(value);
        }
    }

    [Test]
    public async Task ToString_AllExponentsRoundtrip()
    {
        for (int exp = -1022; exp <= 1023; exp++)
        {
            long bits = ((long)(exp + 1023) << 52);
            double value = BitConverter.Int64BitsToDouble(bits);
            if (double.IsInfinity(value) || double.IsNaN(value))
            {
                continue;
            }

            string str = Zmij.ToString(value);
            double parsed = double.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture);
            await Assert.That(parsed).IsEqualTo(value);
        }
    }

    [Test]
    public async Task ToString_RoundingEdgeCasesRoundtrip()
    {
        double[] edgeCases =
        [
            0.9999999999999999,
            1.0000000000000002,
            2.9999999999999996,
            1e23,
            1e-23,
            1.2345678901234567e100,
            9.999999999999999e-5,
            1.0000000000000001e-100,
            Math.PI * 1e22,
            Math.PI * 1e-22,
            1e-5,
            1e-4,
            1e-3,
            1e22,
            1e23,
        ];

        foreach (var value in edgeCases)
        {
            string str = Zmij.ToString(value);
            double parsed = double.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture);
            await Assert.That(parsed).IsEqualTo(value);
        }
    }

    [Test]
    public async Task ToString_Ieee754SpecialPatternsRoundtrip()
    {
        ulong[] specialBits =
        [
            0x0000000000000000UL,
            0x8000000000000000UL,
            0x7FF0000000000000UL,
            0xFFF0000000000000UL,
            0x7FF0000000000001UL,
            0xFFF0000000000001UL,
            0x7FF8000000000000UL,
            0xFFF8000000000000UL,
            0x0010000000000000UL,
            0x8010000000000000UL,
            0x000FFFFFFFFFFFFFUL,
            0x800FFFFFFFFFFFFFUL,
            0x7FEFFFFFFFFFFFFFUL,
            0xFFEFFFFFFFFFFFFFUL,
            0x3FF0000000000000UL,
            0xBFF0000000000000UL,
        ];

        foreach (ulong bits in specialBits)
        {
            double value = BitConverter.Int64BitsToDouble((long)bits);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                continue;
            }

            string str = Zmij.ToString(value);
            double parsed = double.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture);
            await Assert.That(parsed).IsEqualTo(value);
        }
    }

    [Test]
    public async Task ToDecimal_Zero_ReturnsCorrectStruct()
    {
        ZmijDecimal dec = Zmij.ToDecimal(0.0);
        await Assert.That(dec.Significand).IsEqualTo(0);
        await Assert.That(dec.Exponent).IsEqualTo(0);
        await Assert.That(dec.IsNegative).IsFalse();
    }
}
