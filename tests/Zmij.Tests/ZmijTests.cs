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

    // ============================================================
    // Ported from https://github.com/vitaut/zmij/blob/main/test/zmij-test.cc
    // ============================================================

    [Test]
    public async Task Normal_PlanckConstant()
    {
        await Assert.That(Zmij.ToString(6.62607015e-34)).IsEqualTo("6.62607015e-34");
    }

    [Test]
    public async Task Normal_ExactHalfUlpTie()
    {
        await Assert.That(Zmij.ToString(5.444310685350916e+14)).IsEqualTo("544431068535091.6");
    }

    [Test]
    public async Task Subnormal_DenormMin()
    {
        await Assert.That(Zmij.ToString(double.Epsilon)).IsEqualTo("5e-324");
    }

    [Test]
    [Arguments(1e-323, "1e-323")]
    [Arguments(1.2e-322, "1.2e-322")]
    [Arguments(1.5e-323, "1.5e-323")]
    [Arguments(1.24e-322, "1.24e-322")]
    [Arguments(1.234e-320, "1.234e-320")]
    [Arguments(2.2250738585072004e-308, "2.2250738585072004e-308")]
    public async Task Subnormal_KnownValues(double value, string expected)
    {
        await Assert.That(Zmij.ToString(value)).IsEqualTo(expected);
    }

    [Test]
    public async Task AllIrregularExponents_MatchExpected()
    {
        string[] fixedValues =
        [
            "0.0001220703125",
            "0.000244140625",
            "0.00048828125",
            "0.0009765625",
            "0.001953125",
            "0.00390625",
            "0.0078125",
            "0.015625",
            "0.03125",
            "0.0625",
            "0.125",
            "0.25",
            "0.5",
        ];

        int fixedStart = 1010, fixedEnd = 1022;
        for (ulong exp = 1; exp < 0x3ff; exp++)
        {
            ulong bits = exp << 52;
            double value = BitConverter.Int64BitsToDouble((long)bits);

            if (exp >= (ulong)fixedStart && exp <= (ulong)fixedEnd)
            {
                await Assert.That(Zmij.ToString(value)).IsEqualTo(fixedValues[(int)(exp - (ulong)fixedStart)]);
                continue;
            }

            string str = Zmij.ToString(value);
            bool parsed = double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double roundtrip);
            await Assert.That(parsed).IsTrue();
            double diff = Math.Abs(roundtrip - value) / Math.Max(1.0, Math.Abs(value));
            await Assert.That(diff).IsLessThanOrEqualTo(1e-15);
        }
    }

    [Test]
    public async Task AllExponentsWithLsbSet_MatchExpected()
    {
        string[] fixedValues =
        [
            "0.00012207031250000003",
            "0.00024414062500000005",
            "0.0004882812500000001",
            "0.0009765625000000002",
            "0.0019531250000000004",
            "0.003906250000000001",
            "0.007812500000000002",
            "0.015625000000000003",
            "0.03125000000000001",
            "0.06250000000000001",
            "0.12500000000000003",
            "0.25000000000000006",
            "0.5000000000000001",
            "1.0000000000000002",
        ];

        int fixedStart = 1010, fixedEnd = 1023;
        for (ulong exp = 0; exp <= 0x3ff; exp++)
        {
            ulong bits = (exp << 52) | 1;
            double value = BitConverter.Int64BitsToDouble((long)bits);

            if (exp >= (ulong)fixedStart && exp <= (ulong)fixedEnd)
            {
                await Assert.That(Zmij.ToString(value)).IsEqualTo(fixedValues[(int)(exp - (ulong)fixedStart)]);
                continue;
            }

            string str = Zmij.ToString(value);
            bool parsed = double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double roundtrip);
            await Assert.That(parsed).IsTrue();
            double diff = Math.Abs(roundtrip - value) / Math.Max(1.0, Math.Abs(value));
            await Assert.That(diff).IsLessThanOrEqualTo(1e-15);
        }
    }

    [Test]
    public async Task Shorter_Underestimate()
    {
        await Assert.That(Zmij.ToString(-4.932096661796888e-226)).IsEqualTo("-4.932096661796888e-226");
    }

    [Test]
    public async Task Shorter_Overestimate()
    {
        await Assert.That(Zmij.ToString(3.439070283483335e+35)).IsEqualTo("3.439070283483335e+35");
    }

    [Test]
    public async Task SingleCandidate_Underestimate()
    {
        await Assert.That(Zmij.ToString(6.606854224493745e-17)).IsEqualTo("6.606854224493745e-17");
    }

    [Test]
    public async Task SingleCandidate_Overestimate()
    {
        await Assert.That(Zmij.ToString(6.079537928711555e+61)).IsEqualTo("6.079537928711555e+61");
    }

    [Test]
    [Arguments(1.3588129002659584e-245, "1.3588129002659584e-245")]
    [Arguments(2.9802322387695312e-08, "2.9802322387695312e-08")]
    [Arguments(5.960464477539063e-08, "5.960464477539063e-08")]
    [Arguments(1.3076622631878654e+65, "1.3076622631878654e+65")]
    [Arguments(9.03725590277404e+159, "9.03725590277404e+159")]
    [Arguments(9.03725590277404e+160, "9.03725590277404e+160")]
    [Arguments(9.03725590277404e+161, "9.03725590277404e+161")]
    [Arguments(9.03725590277404e+162, "9.03725590277404e+162")]
    [Arguments(5.960464477539062e-07, "5.960464477539062e-07")]
    public async Task BoundaryCases(double value, string expected)
    {
        await Assert.That(Zmij.ToString(value)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(43210.0, "43210")]
    [Arguments(43210.1, "43210.1")]
    [Arguments(10000.0, "10000")]
    [Arguments(-5942736479622170.0, "-5942736479622170")]
    public async Task FixedWithZeros(double value, string expected)
    {
        await Assert.That(Zmij.ToString(value)).IsEqualTo(expected);
    }

    [Test]
    public async Task NoUnderrun()
    {
        string result = Zmij.ToString(9.061488e+15);
        double parsed = double.Parse(result, NumberStyles.Float, CultureInfo.InvariantCulture);
        await Assert.That(parsed).IsEqualTo(9.061488e+15);
    }

    [Test]
    public async Task NoOverrun()
    {
        const int BufferSize = 34;
        byte[] buffer = new byte[BufferSize + 1];
        buffer.AsSpan().Fill((byte)'?');
        bool success = Zmij.TryWrite(-1.2345678901234567e+123, buffer.AsSpan(0, BufferSize), out int bytesWritten);
        await Assert.That(success).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(buffer[..bytesWritten])).IsEqualTo("-1.2345678901234567e+123");
        await Assert.That(buffer[BufferSize]).IsEqualTo((byte)'?');
    }

    [Test]
    public async Task ToDecimal_KnownValue()
    {
        ZmijDecimal dec = Zmij.ToDecimal(6.62607015e-34);
        await Assert.That(dec.Significand).IsEqualTo(66260701500000000);
        await Assert.That(dec.Exponent).IsEqualTo(-50);
        await Assert.That(dec.IsNegative).IsFalse();
    }

    [Test]
    public async Task ToDecimal_NegativeValue()
    {
        ZmijDecimal dec = Zmij.ToDecimal(-6.62607015e-34);
        await Assert.That(dec.Significand).IsEqualTo(66260701500000000);
        await Assert.That(dec.Exponent).IsEqualTo(-50);
        await Assert.That(dec.IsNegative).IsTrue();
    }

    [Test]
    public async Task ToDecimal_NegativeZero()
    {
        ZmijDecimal dec = Zmij.ToDecimal(-0.0);
        await Assert.That(dec.Significand).IsEqualTo(0);
        await Assert.That(dec.Exponent).IsEqualTo(0);
        await Assert.That(dec.IsNegative).IsTrue();
    }
}
