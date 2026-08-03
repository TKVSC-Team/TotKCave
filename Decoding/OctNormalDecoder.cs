using System.Numerics;

namespace TotkCave.Decoding;

public static class OctNormalDecoder
{
    public static Vector3 Decode(int nu, int nv)
    {
        float x = (nu / 127.0f) * 2.0f - 1.0f;
        float y = (nv / 127.0f) * 2.0f - 1.0f;
        float z = 1.0f - MathF.Abs(x) - MathF.Abs(y);

        if (z < 0.0f)
        {
            float ox = x;
            x = (1.0f - MathF.Abs(y)) * (ox >= 0.0f ? 1.0f : -1.0f);
            y = (1.0f - MathF.Abs(ox)) * (nv >= 63.5f ? 1.0f : -1.0f);
        }

        float lengthSq = x * x + y * y + z * z;
        float invLength = lengthSq > 1e-6f ? 1.0f / MathF.Sqrt(lengthSq) : 1.0f;

        return new Vector3(x * invLength, y * invLength, z * invLength);
    }
}
