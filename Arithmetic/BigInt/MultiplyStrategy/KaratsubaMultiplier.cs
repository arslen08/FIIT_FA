using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class KaratsubaMultiplier : IMultiplier
{
    private const int Threshold = 32;

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        uint[] product = MultiplyMagnitudes(a.GetDigits(), b.GetDigits());
        bool negative = a.IsNegative ^ b.IsNegative;
        return new BetterBigInteger(product, negative); 
    }

    private static uint[] MultiplyMagnitudes(ReadOnlySpan<uint> x, ReadOnlySpan<uint> y)
    {
        int m = EffectiveLength(x);
        int n = EffectiveLength(y);

        if (m == 0 || n == 0)
            return [0u];

        x = x[..m];
        y = y[..n];

        if (Math.Min(m, n) <= Threshold)
            return Schoolbook(x, y);

        int half = Math.Max(m, n) / 2;

        ReadOnlySpan<uint> x0 = x[..Math.Min(half, m)];
        ReadOnlySpan<uint> x1 = half < m ? x[half..] : default;
        ReadOnlySpan<uint> y0 = y[..Math.Min(half, n)];
        ReadOnlySpan<uint> y1 = half < n ? y[half..] : default;

        uint[] z0 = MultiplyMagnitudes(x0, y0);            
        uint[] z2 = MultiplyMagnitudes(x1, y1);            

        uint[] sumX = AddMagnitude(x0, x1);               
        uint[] sumY = AddMagnitude(y0, y1);                
        uint[] z1 = MultiplyMagnitudes(sumX, sumY);        

        z1 = SubtractMagnitude(z1, z0);
        z1 = SubtractMagnitude(z1, z2);

        var result = new uint[m + n + 1];
        AddInPlace(result, z0, 0);
        AddInPlace(result, z1, half);
        AddInPlace(result, z2, 2 * half);
        return Trim(result);
    }

    private static uint[] Schoolbook(ReadOnlySpan<uint> x, ReadOnlySpan<uint> y)
    {
        int m = EffectiveLength(x);
        int n = EffectiveLength(y);
        if (m == 0 || n == 0)
            return [0u];

        var result = new uint[m + n];
        for (int i = 0; i < m; i++)
        {
            ulong factor = x[i];
            if (factor == 0)
                continue;

            ulong carry = 0;
            int k = i;
            for (int j = 0; j < n; j++)
            {
                ulong current = result[k] + factor * y[j] + carry;
                result[k] = (uint)current;
                carry = current >> 32;
                k++;
            }
            while (carry != 0)
            {
                ulong current = result[k] + carry;
                result[k] = (uint)current;
                carry = current >> 32;
                k++;
            }
        }
        return Trim(result);
    }

    private static void AddInPlace(uint[] result, ReadOnlySpan<uint> value, int limbOffset)
    {
        int len = EffectiveLength(value);
        ulong carry = 0;
        int i = 0;
        for (; i < len; i++)
        {
            ulong current = result[limbOffset + i] + (ulong)value[i] + carry;
            result[limbOffset + i] = (uint)current;
            carry = current >> 32;
        }
        int k = limbOffset + len;
        while (carry != 0)
        {
            ulong current = result[k] + carry;
            result[k] = (uint)current;
            carry = current >> 32;
            k++;
        }
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
            ulong current = carry + (i < la ? a[i] : 0u) + (i < lb ? b[i] : 0u);
            result[i] = (uint)current;
            carry = current >> 32;
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
            long current = (long)a[i] - (i < lb ? b[i] : 0u) - borrow;
            if (current < 0)
            {
                current += 1L << 32;
                borrow = 1;
            }
            else
            {
                borrow = 0;
            }
            result[i] = (uint)current;
        }
        return Trim(result);
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
}