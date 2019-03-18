using System.IO;

namespace System
{
    public unsafe static class Memory
    {
        public static unsafe void Move(VoidPtr dst, VoidPtr src, uint size)
        {
            byte* from = (byte*)src.address;
            byte* to = (byte*)dst.address;
            if (src < dst)
                for (uint i = size - 1; i >= 0; i--)
                    to[i] = from[i];
            else if (src > dst)
                for (uint i = 0; i < size; i++)
                    to[i] = from[i];
        }

        internal static unsafe void Fill(VoidPtr dest, uint length, byte value)
        {
            byte* to = (byte*)dest.address;
            for (uint i = 0; i < length; i++)
                to[i] = value;
        }
    }
}
