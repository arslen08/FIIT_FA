using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    private const int Base16LimbLimit = 1024;

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

        int bits = Math.Max(m, n) <= Base16LimbLimit ? 16 : 8;
        int coeffsPerLimb = 32 / bits;
        uint mask = (1u << bits) - 1;

        int aLen = m * coeffsPerLimb;
        int bLen = n * coeffsPerLimb;

        int size = 1;
        while (size < aLen + bLen)
            size <<= 1;

        var areal = new double[size];
        var aimag = new double[size];
        var breal = new double[size];
        var bimag = new double[size];

        Decompose(x, m, bits, coeffsPerLimb, mask, areal);
        Decompose(y, n, bits, coeffsPerLimb, mask, breal);

        Fft(areal, aimag, invert: false);
        Fft(breal, bimag, invert: false);

        for (int i = 0; i < size; i++)
        {
            double re = areal[i] * breal[i] - aimag[i] * bimag[i];
            double im = areal[i] * bimag[i] + aimag[i] * breal[i];
            areal[i] = re;
            aimag[i] = im;
        }

        Fft(areal, aimag, invert: true);

        var digits = new int[size + 4];
        long carry = 0;
        int count = 0;
        for (int i = 0; i < size; i++)
        {
            long value = (long)Math.Round(areal[i]) + carry;
            digits[count++] = (int)(value & mask);
            carry = value >> bits;
        }
        while (carry != 0)
        {
            digits[count++] = (int)(carry & mask);
            carry >>= bits;
        }

        int limbCount = (count + coeffsPerLimb - 1) / coeffsPerLimb;
        var result = new uint[limbCount];
        for (int i = 0; i < limbCount; i++)
        {
            uint limb = 0;
            for (int k = 0; k < coeffsPerLimb; k++)
            {
                int idx = i * coeffsPerLimb + k;
                if (idx < count)
                    limb |= (uint)digits[idx] << (bits * k);
            }
            result[i] = limb;
        }

        return Trim(result);
    }

    private static void Decompose(ReadOnlySpan<uint> value, int len, int bits, int coeffsPerLimb, uint mask, double[] dest)
    {
        int pos = 0;
        for (int i = 0; i < len; i++)
        {
            uint limb = value[i];
            for (int k = 0; k < coeffsPerLimb; k++)
                dest[pos++] = (limb >> (bits * k)) & mask;
        }
    }

    private static void Fft(double[] re, double[] im, bool invert)
    {
        int n = re.Length;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = 2.0 * Math.PI / len * (invert ? 1 : -1);
            double wLenRe = Math.Cos(angle);
            double wLenIm = Math.Sin(angle);
            int half = len >> 1;

            for (int i = 0; i < n; i += len)
            {
                double wRe = 1.0;
                double wIm = 0.0;
                for (int k = 0; k < half; k++)
                {
                    int u = i + k;
                    int v = i + k + half;

                    double vRe = re[v] * wRe - im[v] * wIm;
                    double vIm = re[v] * wIm + im[v] * wRe;

                    re[v] = re[u] - vRe;
                    im[v] = im[u] - vIm;
                    re[u] += vRe;
                    im[u] += vIm;

                    double nextWRe = wRe * wLenRe - wIm * wLenIm;
                    wIm = wRe * wLenIm + wIm * wLenRe;
                    wRe = nextWRe;
                }
            }
        }

        if (invert)
            for (int i = 0; i < n; i++)
            {
                re[i] /= n;
                im[i] /= n;
            }
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