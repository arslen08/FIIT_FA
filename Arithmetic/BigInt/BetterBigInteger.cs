using System.Numerics;
using System.Text;
using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    private const int Base32 = 32;
    private const ulong Base = 1UL << 32;
    private const uint SignMask = 0x8000_0000u;

    private const int KaratsubaThreshold = 32;   
    private const int FftThreshold = 1024;        

    private int _signBit;

    private uint _smallValue; // Если число маленькое, храним его прямо в этом поле, а _data == null.
    private uint[]? _data;

    public bool IsNegative => _signBit == 1;

    public bool IsZero => _data is null && _smallValue == 0;

    public static BetterBigInteger Zero { get; } = new(0u, null, 0);
    public static BetterBigInteger One { get; } = new(1u, null, 0);

    private BetterBigInteger(uint smallValue, uint[]? data, int signBit)
    {
        _smallValue = smallValue;
        _data = data;
        _signBit = signBit;
    }

    /// От массива цифр (little endian)
    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        ArgumentNullException.ThrowIfNull(digits);
        var canonical = Create(digits, isNegative);
        _smallValue = canonical._smallValue;
        _data = canonical._data;
        _signBit = canonical._signBit;
    }

    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
    {
        ArgumentNullException.ThrowIfNull(digits);
        uint[] array = digits as uint[] ?? digits.ToArray();
        var canonical = Create(array, isNegative);
        _smallValue = canonical._smallValue;
        _data = canonical._data;
        _signBit = canonical._signBit;
    }

    public BetterBigInteger(string value, int radix)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (radix is < 2 or > 36)
            throw new ArgumentOutOfRangeException(nameof(radix), radix, "Основание системы счисления должно быть в диапазоне [2; 36].");

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        if (span.IsEmpty)
            throw new FormatException("Пустая строка не является корректным числом.");

        bool negative = false;
        int start = 0;
        if (span[0] is '+' or '-')
        {
            negative = span[0] == '-';
            start = 1;
        }

        if (start == span.Length)
            throw new FormatException("Строка не содержит ни одной цифры.");

        uint[] magnitude = [0u];
        for (int i = start; i < span.Length; i++)
        {
            int digit = CharToDigit(span[i]);
            if (digit < 0 || digit >= radix)
                throw new FormatException($"Символ '{span[i]}' недопустим для системы счисления с основанием {radix}.");

            magnitude = MultiplySmall(magnitude, (uint)radix);
            magnitude = AddSmall(magnitude, (uint)digit);
        }

        var canonical = Create(magnitude, negative);
        _smallValue = canonical._smallValue;
        _data = canonical._data;
        _signBit = canonical._signBit;
    }

    public ReadOnlySpan<uint> GetDigits()
    {
        return _data ?? [_smallValue];
    }

    private uint[] MagnitudeArray() => _data ?? [_smallValue];

    private static BetterBigInteger Create(ReadOnlySpan<uint> magnitude, bool negative)
    {
        int len = EffectiveLength(magnitude);
        if (len == 0)
            return Zero;

        int sign = negative ? 1 : 0;
        if (len == 1)
            return new BetterBigInteger(magnitude[0], null, sign);

        var data = new uint[len];
        magnitude[..len].CopyTo(data);
        return new BetterBigInteger(0u, data, sign);
    }

    public int CompareTo(IBigInteger? other)
    {
        if (other is null)
            return 1;

        ReadOnlySpan<uint> otherDigits = other.GetDigits();
        bool otherZero = EffectiveLength(otherDigits) == 0;
        bool thisZero = IsZero;

        if (thisZero && otherZero)
            return 0;

        bool thisNeg = !thisZero && IsNegative;
        bool otherNeg = !otherZero && other.IsNegative;

        if (thisNeg && !otherNeg) return -1;
        if (!thisNeg && otherNeg) return 1;

        int cmp = CompareMagnitude(GetDigits(), otherDigits);
        return thisNeg ? -cmp : cmp; 
    }

    public bool Equals(IBigInteger? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return CompareTo(other) == 0;
    }

    public override bool Equals(object? obj) => obj is IBigInteger other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(!IsZero && IsNegative);

        ReadOnlySpan<uint> digits = GetDigits();
        int len = EffectiveLength(digits);
        hash.Add(len);
        for (int i = 0; i < len; i++)
            hash.Add(digits[i]);

        return hash.ToHashCode();
    }

    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return AddSigned(a, a.IsNegative, b, b.IsNegative);
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return AddSigned(a, a.IsNegative, b, !b.IsNegative);
    }

    public static BetterBigInteger operator -(BetterBigInteger a)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (a.IsZero)
            return Zero;
        return Create(a.GetDigits(), !a.IsNegative);
    }

    private static BetterBigInteger AddSigned(BetterBigInteger a, bool aNeg, BetterBigInteger b, bool bNeg)
    {
        ReadOnlySpan<uint> am = a.GetDigits();
        ReadOnlySpan<uint> bm = b.GetDigits();

        if (a.IsZero)
            return Create(bm, bNeg);
        if (b.IsZero)
            return Create(am, aNeg);

        if (aNeg == bNeg)
            return Create(AddMagnitude(am, bm), aNeg);

        int cmp = CompareMagnitude(am, bm);
        if (cmp == 0)
            return Zero;
        return cmp > 0
            ? Create(SubtractMagnitude(am, bm), aNeg)
            : Create(SubtractMagnitude(bm, am), bNeg);
    }

    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        IMultiplier strategy = SelectMultiplier(a, b);
        return strategy.Multiply(a, b);
    }

    private static IMultiplier SelectMultiplier(BetterBigInteger a, BetterBigInteger b)
    {
        int size = Math.Max(a.GetDigits().Length, b.GetDigits().Length);

        if (size >= FftThreshold)
            return new FftMultiplier();
        if (size >= KaratsubaThreshold)
            return new KaratsubaMultiplier();
        return new SimpleMultiplier();
    }

    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (b.IsZero)
            throw new DivideByZeroException();
        if (a.IsZero)
            return Zero;

        ReadOnlySpan<uint> am = a.GetDigits();
        ReadOnlySpan<uint> bm = b.GetDigits();
        if (CompareMagnitude(am, bm) < 0)
            return Zero; 

        (uint[] quotient, _) = DivModMagnitude(am, bm);
        return Create(quotient, a.IsNegative ^ b.IsNegative); 
    }

    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (b.IsZero)
            throw new DivideByZeroException();
        if (a.IsZero)
            return Zero;

        ReadOnlySpan<uint> am = a.GetDigits();
        ReadOnlySpan<uint> bm = b.GetDigits();
        if (CompareMagnitude(am, bm) < 0)
            return a; 

        (_, uint[] remainder) = DivModMagnitude(am, bm);
        return Create(remainder, a.IsNegative); 
    }

    public static BetterBigInteger operator ~(BetterBigInteger a)
    {
        ArgumentNullException.ThrowIfNull(a);
        ReadOnlySpan<uint> am = a.GetDigits();
        int n = EffectiveLength(am) + 1;
        uint[] t = ToTwosComplement(am, a.IsNegative, n);
        for (int i = 0; i < n; i++)
            t[i] = ~t[i];
        return FromTwosComplement(t);
    }

    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
        => Bitwise(a, b, static (x, y) => x & y);

    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
        => Bitwise(a, b, static (x, y) => x | y);

    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
        => Bitwise(a, b, static (x, y) => x ^ y);

    private static BetterBigInteger Bitwise(BetterBigInteger a, BetterBigInteger b, Func<uint, uint, uint> op)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        ReadOnlySpan<uint> am = a.GetDigits();
        ReadOnlySpan<uint> bm = b.GetDigits();
        int n = Math.Max(EffectiveLength(am), EffectiveLength(bm)) + 1;

        uint[] ta = ToTwosComplement(am, a.IsNegative, n);
        uint[] tb = ToTwosComplement(bm, b.IsNegative, n);

        var result = new uint[n];
        for (int i = 0; i < n; i++)
            result[i] = op(ta[i], tb[i]);

        return FromTwosComplement(result);
    }

    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (shift == 0)
            return a;
        if (shift < 0)
            return ShiftRight(a, -(long)shift);
        return ShiftLeft(a, shift);
    }

    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (shift == 0)
            return a;
        if (shift < 0)
            return ShiftLeft(a, -(long)shift);
        return ShiftRight(a, shift);
    }

    private static BetterBigInteger ShiftLeft(BetterBigInteger a, long bits)
    {
        if (a.IsZero || bits == 0)
            return a.IsZero ? Zero : a;
        if (bits > int.MaxValue)
            throw new OverflowException("Слишком большой сдвиг влево.");

        uint[] mag = ShiftLeftBits(a.GetDigits(), (int)bits);
        return Create(mag, a.IsNegative);
    }

    private static BetterBigInteger ShiftRight(BetterBigInteger a, long bits)
    {
        if (a.IsZero || bits == 0)
            return a.IsZero ? Zero : a;
        if (bits > int.MaxValue)
            return a.IsNegative ? Create([1u], true) : Zero; 

        int b = (int)bits;
        ReadOnlySpan<uint> mag = a.GetDigits();
        uint[] shifted = ShiftRightBits(mag, b);

        if (!a.IsNegative)
            return Create(shifted, false);

        if (HasLowBitsSet(mag, b))
            shifted = AddSmall(shifted, 1u);
        return Create(shifted, true);
    }

    public static bool operator ==(BetterBigInteger a, BetterBigInteger b) => Equals(a, b);
    public static bool operator !=(BetterBigInteger a, BetterBigInteger b) => !Equals(a, b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;

    public override string ToString() => ToString(10);

    public string ToString(int radix)
    {
        if (radix is < 2 or > 36)
            throw new ArgumentOutOfRangeException(nameof(radix), radix, "Основание системы счисления должно быть в диапазоне [2; 36].");

        if (IsZero)
            return "0";

        uint[] current = MagnitudeArray();
        var digits = new StringBuilder();
        while (EffectiveLength(current) != 0)
        {
            (current, uint remainder) = DivModSmall(current, (uint)radix);
            digits.Append(DigitToChar((int)remainder));
        }

        int length = digits.Length;
        var chars = new char[IsNegative ? length + 1 : length];
        int offset = 0;
        if (IsNegative)
            chars[offset++] = '-';
        for (int i = 0; i < length; i++)
            chars[offset + i] = digits[length - 1 - i];

        return new string(chars);
    }

    private static int EffectiveLength(ReadOnlySpan<uint> value)
    {
        for (int i = value.Length - 1; i >= 0; i--)
            if (value[i] != 0)
                return i + 1;
        return 0;
    }

    private static uint[] Trim(uint[] value)
    {
        int len = EffectiveLength(value);
        if (len == 0)
            return [0u];
        if (len == value.Length)
            return value;
        var trimmed = new uint[len];
        Array.Copy(value, trimmed, len);
        return trimmed;
    }

    private static int CompareMagnitude(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int la = EffectiveLength(a);
        int lb = EffectiveLength(b);
        if (la != lb)
            return la < lb ? -1 : 1;
        for (int i = la - 1; i >= 0; i--)
            if (a[i] != b[i])
                return a[i] < b[i] ? -1 : 1;
        return 0;
    }

    private static uint[] AddMagnitude(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int la = EffectiveLength(a);
        int lb = EffectiveLength(b);
        int n = Math.Max(la, lb);
        var result = new uint[n + 1];
        ulong carry = 0;
        for (int i = 0; i < n; i++)
        {
            ulong cur = carry + (i < la ? a[i] : 0u) + (i < lb ? b[i] : 0u);
            result[i] = (uint)cur;
            carry = cur >> 32;
        }
        result[n] = (uint)carry;
        return Trim(result);
    }

    private static uint[] SubtractMagnitude(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int la = EffectiveLength(a);
        int lb = EffectiveLength(b);
        var result = new uint[la];
        long borrow = 0;
        for (int i = 0; i < la; i++)
        {
            long cur = (long)a[i] - (i < lb ? b[i] : 0u) - borrow;
            if (cur < 0)
            {
                cur += (long)Base;
                borrow = 1;
            }
            else
            {
                borrow = 0;
            }
            result[i] = (uint)cur;
        }
        return Trim(result);
    }

    private static uint[] MultiplySmall(ReadOnlySpan<uint> value, uint factor)
    {
        int n = EffectiveLength(value);
        if (n == 0 || factor == 0)
            return [0u];
        var result = new uint[n + 1];
        ulong carry = 0;
        for (int i = 0; i < n; i++)
        {
            ulong cur = (ulong)value[i] * factor + carry;
            result[i] = (uint)cur;
            carry = cur >> 32;
        }
        result[n] = (uint)carry;
        return Trim(result);
    }

    private static uint[] AddSmall(ReadOnlySpan<uint> value, uint addend)
    {
        int n = EffectiveLength(value);
        var result = new uint[n + 1];
        ulong carry = addend;
        for (int i = 0; i < n; i++)
        {
            ulong cur = value[i] + carry;
            result[i] = (uint)cur;
            carry = cur >> 32;
        }
        result[n] = (uint)carry;
        return Trim(result);
    }

    private static (uint[] quotient, uint remainder) DivModSmall(ReadOnlySpan<uint> u, uint d)
    {
        int n = EffectiveLength(u);
        if (n == 0)
            return ([0u], 0u);
        var q = new uint[n];
        ulong rem = 0;
        for (int i = n - 1; i >= 0; i--)
        {
            ulong cur = (rem << 32) | u[i];
            q[i] = (uint)(cur / d);
            rem = cur % d;
        }
        return (Trim(q), (uint)rem);
    }

    private static (uint[] quotient, uint[] remainder) DivModMagnitude(ReadOnlySpan<uint> uIn, ReadOnlySpan<uint> vIn)
    {
        int m = EffectiveLength(uIn);
        int n = EffectiveLength(vIn);

        if (n == 0)
            throw new DivideByZeroException();
        if (m < n)
            return ([0u], Trim(uIn.ToArray()));
        if (n == 1)
        {
            (uint[] q1, uint rem) = DivModSmall(uIn, vIn[0]);
            return (q1, [rem]);
        }

        int s = BitOperations.LeadingZeroCount(vIn[n - 1]); 
        uint[] vn = PadTo(ShiftLeftBits(vIn[..n], s), n);
        uint[] un = PadTo(ShiftLeftBits(uIn[..m], s), m + 1);

        var q = new uint[m - n + 1];

        for (int j = m - n; j >= 0; j--)
        {
            ulong numerator = (ulong)un[j + n] * Base + un[j + n - 1];
            ulong qhat = numerator / vn[n - 1];
            ulong rhat = numerator - qhat * vn[n - 1];

            while (qhat >= Base || qhat * vn[n - 2] > Base * rhat + un[j + n - 2])
            {
                qhat--;
                rhat += vn[n - 1];
                if (rhat >= Base)
                    break;
            }

            long k = 0;
            long t = 0;
            for (int i = 0; i < n; i++)
            {
                ulong p = qhat * vn[i];
                t = un[i + j] - k - (long)(uint)(p & 0xFFFF_FFFFUL);
                un[i + j] = (uint)t;
                k = (long)(p >> 32) - (t >> 32);
            }
            t = un[j + n] - k;
            un[j + n] = (uint)t;

            q[j] = (uint)qhat;

            if (t < 0)
            {
                q[j]--;
                long carry = 0;
                for (int i = 0; i < n; i++)
                {
                    ulong sum = (ulong)un[i + j] + vn[i] + (ulong)carry;
                    un[i + j] = (uint)sum;
                    carry = (long)(sum >> 32);
                }
                un[j + n] = (uint)(un[j + n] + (uint)carry);
            }
        }

        var remNorm = new uint[n];
        Array.Copy(un, remNorm, n);
        uint[] remainder = ShiftRightBits(remNorm, s);

        return (Trim(q), Trim(remainder));
    }

    private static uint[] PadTo(uint[] value, int length)
    {
        if (value.Length == length)
            return value;
        var padded = new uint[length];
        Array.Copy(value, padded, Math.Min(value.Length, length));
        return padded;
    }

    private static uint[] ShiftLeftBits(ReadOnlySpan<uint> value, int bits)
    {
        int n = EffectiveLength(value);
        if (n == 0 || bits == 0)
            return Trim(value.ToArray());

        int limbShift = bits / Base32;
        int bitShift = bits % Base32;
        var result = new uint[n + limbShift + 1];

        for (int i = 0; i < n; i++)
        {
            ulong v = (ulong)value[i] << bitShift;
            result[i + limbShift] |= (uint)v;
            result[i + limbShift + 1] |= (uint)(v >> 32);
        }
        return Trim(result);
    }

    private static uint[] ShiftRightBits(ReadOnlySpan<uint> value, int bits)
    {
        int n = EffectiveLength(value);
        if (n == 0)
            return [0u];
        if (bits == 0)
            return Trim(value.ToArray());

        int limbShift = bits / Base32;
        int bitShift = bits % Base32;
        if (limbShift >= n)
            return [0u];

        int newLen = n - limbShift;
        var result = new uint[newLen];
        for (int i = 0; i < newLen; i++)
        {
            ulong v = (ulong)value[i + limbShift] >> bitShift;
            if (bitShift != 0 && i + limbShift + 1 < n)
                v |= (ulong)value[i + limbShift + 1] << (Base32 - bitShift);
            result[i] = (uint)v;
        }
        return Trim(result);
    }

    private static bool HasLowBitsSet(ReadOnlySpan<uint> value, int bits)
    {
        int limbShift = bits / Base32;
        int bitShift = bits % Base32;

        int upper = Math.Min(limbShift, value.Length);
        for (int i = 0; i < upper; i++)
            if (value[i] != 0)
                return true;

        if (bitShift != 0 && limbShift < value.Length)
        {
            uint mask = (1u << bitShift) - 1;
            if ((value[limbShift] & mask) != 0)
                return true;
        }
        return false;
    }

    private static uint[] ToTwosComplement(ReadOnlySpan<uint> magnitude, bool negative, int n)
    {
        var t = new uint[n];
        int len = EffectiveLength(magnitude);
        for (int i = 0; i < len; i++)
            t[i] = magnitude[i];

        if (negative)
        {
            ulong carry = 1;
            for (int i = 0; i < n; i++)
            {
                ulong v = (ulong)(~t[i]) + carry;
                t[i] = (uint)v;
                carry = v >> 32;
            }
        }
        return t;
    }

    private static BetterBigInteger FromTwosComplement(uint[] t)
    {
        bool negative = (t[^1] & SignMask) != 0;
        if (!negative)
            return Create(t, false);

        var magnitude = new uint[t.Length];
        ulong carry = 1;
        for (int i = 0; i < t.Length; i++)
        {
            ulong v = (ulong)(~t[i]) + carry;
            magnitude[i] = (uint)v;
            carry = v >> 32;
        }
        return Create(magnitude, true);
    }

    private static int CharToDigit(char c)
    {
        if (c is >= '0' and <= '9')
            return c - '0';
        if (c is >= 'a' and <= 'z')
            return c - 'a' + 10;
        if (c is >= 'A' and <= 'Z')
            return c - 'A' + 10;
        return -1;
    }

    private static char DigitToChar(int digit)
        => digit < 10 ? (char)('0' + digit) : (char)('a' + digit - 10);
}