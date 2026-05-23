#if NETSTANDARD2_1

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace System
{
    using System.Runtime.CompilerServices;

    internal readonly struct UInt128
    {
        readonly ulong high;
        readonly ulong low;

        UInt128(ulong high, ulong low)
        {
            this.high = high;
            this.low = low;
        }

        public static explicit operator UInt128(ulong value)
        {
            return new UInt128(0, value);
        }

        public static explicit operator ulong(UInt128 value)
        {
            return value.low;
        }

        public static UInt128 operator *(UInt128 left, ulong right)
        {
            ulong high = MultiplyHigh(left.low, right) + left.high * right;
            ulong low = left.low * right;
            return new UInt128(high, low);
        }

        public static UInt128 operator +(UInt128 left, ulong right)
        {
            ulong low = left.low + right;
            return new UInt128(left.high + (low < left.low ? 1UL : 0UL), low);
        }

        public static UInt128 operator >>(UInt128 value, int shift)
        {
            if (shift == 0)
            {
                return value;
            }

            if (shift < 64)
            {
                return new UInt128(
                    value.high >> shift,
                    (value.low >> shift) | (value.high << (64 - shift))
                );
            }

            if (shift < 128)
            {
                return new UInt128(0, value.high >> (shift - 64));
            }

            return new UInt128(0, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong MultiplyHigh(ulong left, ulong right)
        {
            ulong leftLow = (uint)left;
            ulong leftHigh = left >> 32;
            ulong rightLow = (uint)right;
            ulong rightHigh = right >> 32;

            ulong high = leftHigh * rightHigh;
            ulong mid1 = leftLow * rightHigh;
            ulong mid2 = leftHigh * rightLow;
            ulong low = leftLow * rightLow;
            ulong carry = (low >> 32) + (uint)mid1 + (uint)mid2;

            return high + (mid1 >> 32) + (mid2 >> 32) + (carry >> 32);
        }
    }
}

#endif
