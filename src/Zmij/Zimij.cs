using System.Runtime.CompilerServices;
using System.Text;

namespace ZimijNet;

public readonly record struct ZimijDecimal(long Significand, int Exponent, bool IsNegative);

/// <summary>
/// Provides a double-to-string conversion based on zimij algorithm.
///
/// <para>This is a port of the zimij implementation here: https://github.com/vitaut/zmij</para>
/// </summary>
public static class Zimij
{
    const int DoubleBufferSize = 34;
    const int NonFiniteExponent = int.MaxValue;
    const int DoubleSignificandBits = 52;
    const int DoubleExponentBits = 11;
    const int DoubleExponentMask = (1 << DoubleExponentBits) - 1;
    const int DoubleExponentBias = (1 << (DoubleExponentBits - 1)) - 1;
    const int DoubleExponentOffset = DoubleExponentBias + DoubleSignificandBits;
    const ulong DoubleImplicitBit = 1UL << DoubleSignificandBits;
    const int ExtraShift = 6;
    const ulong Threshold = 1_000_000_000_000_000UL;
    const ulong BiasedHalf = 0x7fff_ffff_ffff_ffffUL;

    static readonly byte[] ExpShifts = CreateExpShifts();

    /// <summary>
    /// Converts a double-precision floating-point number to its string representation using the zimij algorithm.
    /// </summary>
    public static string ToString(double value)
    {
        Span<byte> buffer = stackalloc byte[DoubleBufferSize];
        int bytesWritten = Write(value, buffer);
        return Encoding.UTF8.GetString(buffer[..bytesWritten]);
    }

    /// <summary>
    /// Converts a double-precision floating-point number to its UTF-8 byte representation using the zimij algorithm.
    /// </summary>
    public static bool TryWrite(double value, Span<byte> destination, out int bytesWritten)
    {
        Span<byte> buffer = stackalloc byte[DoubleBufferSize];
        bytesWritten = Write(value, buffer);
        if (bytesWritten > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        buffer[..bytesWritten].CopyTo(destination);
        return true;
    }

    /// <summary>
    /// Converts a double-precision floating-point number to a ZimijDecimal,
    /// which contains the significand, exponent, and sign information.
    /// </summary>
    public static ZimijDecimal ToDecimal(double value)
    {
        ulong bits = (ulong)BitConverter.DoubleToInt64Bits(value);
        int binExp = GetExponent(bits);
        ulong binSig = GetSignificand(bits);
        bool negative = IsNegative(bits);

        if (binExp == 0 || binExp == DoubleExponentMask)
        {
            if (binExp != 0)
            {
                return new ZimijDecimal((long)binSig, NonFiniteExponent, negative);
            }

            if (binSig == 0)
            {
                return new ZimijDecimal(0, 0, negative);
            }

            binExp = 1;
            binSig |= DoubleImplicitBit;
        }

        DecimalResult dec = ToDecimal(binSig ^ DoubleImplicitBit, binExp, binSig != 0);
        int lastDigit = dec.HasLastDigit ? dec.LastDigit : 0;
        return new ZimijDecimal(
            (long)(dec.Significand * 10 + (uint)lastDigit),
            dec.Exponent,
            negative
        );
    }

    static int Write(double value, Span<byte> buffer)
    {
        ulong bits = (ulong)BitConverter.DoubleToInt64Bits(value);
        int pos = 0;
        if (IsNegative(bits))
        {
            buffer[pos++] = (byte)'-';
        }

        int binExp = GetExponent(bits);
        ulong binSig = GetSignificand(bits);

        DecimalResult dec;
        bool isNormal = (uint)(binExp - 1) < DoubleExponentMask - 1;
        if (!isNormal)
        {
            if (binExp != 0)
            {
                if (binSig == 0)
                {
                    "inf"u8.CopyTo(buffer[pos..]);
                }
                else
                {
                    "nan"u8.CopyTo(buffer[pos..]);
                }

                return pos + 3;
            }

            if (binSig == 0)
            {
                buffer[pos] = (byte)'0';
                return pos + 1;
            }

            dec = ToDecimal(binSig, 1, regular: true);
            ulong decSig = dec.Significand * 10 + (dec.HasLastDigit ? (uint)dec.LastDigit : 0);
            int decExp = dec.Exponent;
            while (decSig < Threshold)
            {
                decSig *= 10;
                --decExp;
            }

            ulong q = Div10(decSig);
            int lastDigit = (int)(decSig - q * 10);
            dec = new DecimalResult(q, decExp, lastDigit, lastDigit != 0);
        }
        else
        {
            dec = ToDecimal(binSig | DoubleImplicitBit, binExp, binSig != 0);
        }

        bool hasLastDigit = dec.HasLastDigit;
        ulong sig = dec.Significand * 10 + (hasLastDigit ? (uint)dec.LastDigit : 0);
        int exponent = dec.Exponent;
        while (sig != 0)
        {
            ulong q = sig / 10;
            if (sig != q * 10)
            {
                break;
            }

            sig = q;
            ++exponent;
        }

        return pos + WriteDecimal(sig, exponent, buffer[pos..]);
    }

    static int WriteDecimal(ulong significand, int exponent, Span<byte> buffer)
    {
        Span<byte> digits = stackalloc byte[20];
        int digitCount = WriteUInt64(significand, digits);
        int decimalExponent = exponent + digitCount - 1;

        if (decimalExponent >= -4 && decimalExponent <= 15)
        {
            int point = exponent + digitCount;
            int pos = 0;
            if (point <= 0)
            {
                buffer[pos++] = (byte)'0';
                buffer[pos++] = (byte)'.';
                for (int i = 0; i < -point; ++i)
                {
                    buffer[pos++] = (byte)'0';
                }

                digits[..digitCount].CopyTo(buffer[pos..]);
                return pos + digitCount;
            }

            if (point >= digitCount)
            {
                digits[..digitCount].CopyTo(buffer);
                pos = digitCount;
                for (int i = digitCount; i < point; ++i)
                {
                    buffer[pos++] = (byte)'0';
                }

                return pos;
            }

            digits[..point].CopyTo(buffer);
            buffer[point] = (byte)'.';
            digits[point..digitCount].CopyTo(buffer[(point + 1)..]);
            return digitCount + 1;
        }

        int outPos = 0;
        buffer[outPos++] = digits[0];
        if (digitCount > 1)
        {
            buffer[outPos++] = (byte)'.';
            digits[1..digitCount].CopyTo(buffer[outPos..]);
            outPos += digitCount - 1;
        }

        buffer[outPos++] = (byte)'e';
        buffer[outPos++] = decimalExponent >= 0 ? (byte)'+' : (byte)'-';
        uint absExp = (uint)(decimalExponent >= 0 ? decimalExponent : -decimalExponent);
        outPos += WriteUInt32(absExp, buffer[outPos..]);
        return outPos;
    }

    static DecimalResult ToDecimal(ulong binSig, int rawExp, bool regular)
    {
        int binExp = rawExp - DoubleExponentOffset;
        if (!regular)
        {
            int decExp = ComputeDecimalExponent(binExp, regular: false);
            int irregularShift = ComputeExponentShift(binExp, decExp + 1) + ExtraShift;
            UInt128Pair pow10 = GetPowerOf10(-decExp - 1);
            UInt128Pair p = Multiply192High128(pow10.High, pow10.Low, binSig << irregularShift);

            ulong integral = p.High >> ExtraShift;
            ulong fractional = (p.High << (64 - ExtraShift)) | (p.Low >> ExtraShift);

            ulong halfUlp = pow10.High >> (ExtraShift + 1 - irregularShift);
            bool roundUp = halfUlp > ulong.MaxValue - fractional;
            bool roundDown = (halfUlp >> 1) > fractional;
            integral += roundUp ? 1UL : 0UL;

            int digit = (int)Multiply128AddHigh64(fractional, 10, 1UL << 63);
            int lo = (int)Multiply128AddHigh64(fractional - (halfUlp >> 1), 10, ulong.MaxValue);
            if (digit < lo)
            {
                digit = lo;
            }

            return new DecimalResult(
                integral,
                decExp,
                digit,
                (roundUp ? 1 : 0) + (roundDown ? 1 : 0) == 0
            );
        }

        int decimalExp = ComputeDecimalExponent(binExp);
        int shift = ExpShifts[binExp + DoubleExponentOffset];
        ulong even = 1UL - (binSig & 1UL);

        UInt128Pair pow = GetPowerOf10(-decimalExp - 1);
        UInt128Pair product = Multiply192High128(pow.High, pow.Low, binSig << shift);

        ulong integralPart = product.High >> ExtraShift;
        ulong fractionalPart = (product.High << (64 - ExtraShift)) | (product.Low >> ExtraShift);

        ulong halfUlpRegular = (pow.High >> (ExtraShift + 1 - shift)) + even;
        bool roundUpRegular = fractionalPart + halfUlpRegular < fractionalPart;
        bool roundDownRegular = halfUlpRegular > fractionalPart;
        integralPart += roundUpRegular ? 1UL : 0UL;

        int extraDigit = (int)Multiply128AddHigh64(fractionalPart, 10, BiasedHalf);
        if (fractionalPart == (1UL << 62))
        {
            extraDigit = 2;
        }

        return new DecimalResult(
            integralPart,
            decimalExp,
            extraDigit,
            (roundUpRegular ? 1 : 0) + (roundDownRegular ? 1 : 0) == 0
        );
    }

    static UInt128Pair GetPowerOf10(int decimalExp)
    {
        return Pow10Significands[decimalExp + 293];
    }

    static UInt128Pair[] CreatePow10Significands()
    {
        UInt128Pair[] table = new UInt128Pair[618];
        for (int i = 0; i < table.Length; ++i)
        {
            table[i] = ComputePowerOf10((uint)i);
        }

        return table;
    }

    static UInt128Pair ComputePowerOf10(uint i)
    {
        int minorIndex = (int)((i + 10) % (uint)Pow10Minor.Length);
        int majorIndex = (int)((i + 10) / (uint)Pow10Minor.Length);
        ulong m = Pow10Minor[minorIndex];
        UInt128Pair h = Pow10Major[majorIndex];

        ulong h1 = Multiply128High64(h.Low, m);
        ulong c0 = h.Low * m;
        ulong c1 = h1 + h.High * m;
        ulong c2 = (c1 < h1 ? 1UL : 0UL) + Multiply128High64(h.High, m);

        UInt128Pair result =
            (c2 >> 63) != 0
                ? new UInt128Pair(c2, c1)
                : new UInt128Pair((c2 << 1) | (c1 >> 63), (c1 << 1) | (c0 >> 63));
        ulong fixup = (Pow10Fixups[i >> 5] >> (int)(i & 31)) & 1U;
        return new UInt128Pair(result.High, result.Low - fixup);
    }

    static byte[] CreateExpShifts()
    {
        byte[] data = new byte[DoubleExponentMask + 1];
        for (int rawExp = 0; rawExp < data.Length; ++rawExp)
        {
            int binExp = rawExp - DoubleExponentOffset;
            if (rawExp == 0)
            {
                ++binExp;
            }

            int decExp = ComputeDecimalExponent(binExp);
            data[rawExp] = (byte)(ComputeExponentShift(binExp, decExp + 1) + ExtraShift);
        }

        return data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int ComputeDecimalExponent(int binExp, bool regular = true)
    {
        const int log10ThreeOverFourSig = 131_072;
        const int log10TwoSig = 315_653;
        const int log10TwoExp = 20;
        return (binExp * log10TwoSig - (regular ? 0 : log10ThreeOverFourSig)) >> log10TwoExp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int ComputeExponentShift(int binExp, int decimalExp)
    {
        const int log2Pow10Sig = 217_707;
        const int log2Pow10Exp = 16;
        int pow10BinExp = -decimalExp * log2Pow10Sig >> log2Pow10Exp;
        return binExp + pow10BinExp + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static UInt128Pair Multiply192High128(ulong xHigh, ulong xLow, ulong y)
    {
        UInt128 p = (UInt128)xHigh * y;
        ulong pLow = (ulong)p;
        ulong lo = pLow + Multiply128High64(xLow, y);
        return new UInt128Pair((ulong)(p >> 64) + (lo < pLow ? 1UL : 0UL), lo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ulong Multiply128High64(ulong x, ulong y)
    {
        return (ulong)(((UInt128)x * y) >> 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ulong Multiply128AddHigh64(ulong x, ulong y, ulong c)
    {
        return (ulong)((((UInt128)x * y) + c) >> 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ulong Div10(ulong x)
    {
        const ulong div10Sig64 = (1UL << 63) / 5 + 1;
        return Multiply128High64(x, div10Sig64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int WriteUInt64(ulong value, Span<byte> destination)
    {
        Span<byte> tmp = stackalloc byte[20];
        int pos = tmp.Length;
        do
        {
            ulong q = value / 10;
            tmp[--pos] = (byte)('0' + value - q * 10);
            value = q;
        } while (value != 0);

        tmp[pos..].CopyTo(destination);
        return tmp.Length - pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int WriteUInt32(uint value, Span<byte> destination)
    {
        Span<byte> tmp = stackalloc byte[10];
        int pos = tmp.Length;
        do
        {
            uint q = value / 10;
            tmp[--pos] = (byte)('0' + value - q * 10);
            value = q;
        } while (value != 0);

        tmp[pos..].CopyTo(destination);
        return tmp.Length - pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsNegative(ulong bits) => (bits >> 63) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ulong GetSignificand(ulong bits) => bits & (DoubleImplicitBit - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int GetExponent(ulong bits) => (int)((bits << 1) >> (DoubleSignificandBits + 1));

    readonly record struct DecimalResult(
        ulong Significand,
        int Exponent,
        int LastDigit,
        bool HasLastDigit
    );

    readonly record struct UInt128Pair(ulong High, ulong Low);

    static readonly ulong[] Pow10Minor =
    [
        0x8000000000000000,
        0xa000000000000000,
        0xc800000000000000,
        0xfa00000000000000,
        0x9c40000000000000,
        0xc350000000000000,
        0xf424000000000000,
        0x9896800000000000,
        0xbebc200000000000,
        0xee6b280000000000,
        0x9502f90000000000,
        0xba43b74000000000,
        0xe8d4a51000000000,
        0x9184e72a00000000,
        0xb5e620f480000000,
        0xe35fa931a0000000,
        0x8e1bc9bf04000000,
        0xb1a2bc2ec5000000,
        0xde0b6b3a76400000,
        0x8ac7230489e80000,
        0xad78ebc5ac620000,
        0xd8d726b7177a8000,
        0x878678326eac9000,
        0xa968163f0a57b400,
        0xd3c21bcecceda100,
        0x84595161401484a0,
        0xa56fa5b99019a5c8,
        0xcecb8f27f4200f3a,
    ];

    static readonly UInt128Pair[] Pow10Major =
    [
        new(0xaf8e5410288e1b6f, 0x07ecf0ae5ee44dda),
        new(0xb1442798f49ffb4a, 0x99cd11cfdf41779d),
        new(0xb2fe3f0b8599ef07, 0x861fa7e6dcb4aa15),
        new(0xb4bca50b065abe63, 0x0fed077a756b53aa),
        new(0xb67f6455292cbf08, 0x1a3bc84c17b1d543),
        new(0xb84687c269ef3bfb, 0x3d5d514f40eea742),
        new(0xba121a4650e4ddeb, 0x92f34d62616ce413),
        new(0xbbe226efb628afea, 0x890489f70a55368c),
        new(0xbdb6b8e905cb600f, 0x5400e987bbc1c921),
        new(0xbf8fdb78849a5f96, 0xde98520472bdd034),
        new(0xc16d9a0095928a27, 0x75b7053c0f178294),
        new(0xc350000000000000, 0x0000000000000000),
        new(0xc5371912364ce305, 0x6c28000000000000),
        new(0xc722f0ef9d80aad6, 0x424d3ad2b7b97ef6),
        new(0xc913936dd571c84c, 0x03bc3a19cd1e38ea),
        new(0xcb090c8001ab551c, 0x5cadf5bfd3072cc6),
        new(0xcd036837130890a1, 0x36dba887c37a8c10),
        new(0xcf02b2c21207ef2e, 0x94f967e45e03f4bc),
        new(0xd106f86e69d785c7, 0xe13336d701beba52),
        new(0xd31045a8341ca07c, 0x1ede48111209a051),
        new(0xd51ea6fa85785631, 0x552a74227f3ea566),
        new(0xd732290fbacaf133, 0xa97c177947ad4096),
        new(0xd94ad8b1c7380874, 0x18375281ae7822bc),
    ];

    static readonly uint[] Pow10Fixups =
    [
        0x0a4e363f,
        0x00001840,
        0x00006400,
        0x24200040,
        0x00000000,
        0x0c000000,
        0x82c81380,
        0x5e4ce01f,
        0xd730f60f,
        0x0000001b,
        0x00000000,
        0xcdf7fffc,
        0x6e8201d8,
        0x40cd3fd1,
        0xdb642501,
        0x00000d0d,
        0x14042400,
        0x53713840,
        0x11781db4,
        0x00000000,
    ];

    static readonly UInt128Pair[] Pow10Significands = CreatePow10Significands();
}
