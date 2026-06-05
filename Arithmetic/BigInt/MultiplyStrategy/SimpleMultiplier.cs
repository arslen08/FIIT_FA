using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal sealed class SimpleMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        uint[] product = Multiply(a.GetDigits().ToArray(), b.GetDigits().ToArray());

        bool negative = a.IsNegative ^ b.IsNegative;
        return new BetterBigInteger(product, negative); 
    }

    public static uint[] Multiply(uint[] left, uint[] right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        int m = EffectiveLength(left);
        int n = EffectiveLength(right);

        if (m == 0 || n == 0)
            return [0u];

        var result = new uint[m + n];

        for (int i = 0; i < m; i++)
        {
            ulong factor = left[i];
            if (factor == 0)
                continue; 

            ulong carry = 0;
            int k = i; 
            for (int j = 0; j < n; j++)
            {
                ulong current = result[k] + factor * right[j] + carry;
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