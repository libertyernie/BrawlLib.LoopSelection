using System.IO;

namespace System
{
    public unsafe static class Memory
    {
        public static unsafe void Move(VoidPtr dst, VoidPtr src, uint size)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    Win32.MoveMemory(dst, src, size);
                    break;
                case PlatformID.MacOSX:
                    OSX.memmove(dst, src, size);
                    break;
                case PlatformID.Unix:
                    if (Directory.Exists("/Applications")
                        & Directory.Exists("/System")
                        & Directory.Exists("/Users")
                        & Directory.Exists("/Volumes"))
                        goto case PlatformID.MacOSX;
                    else
                        Linux.memmove(dst, src, size);
                    break;
                default:
                    byte* from = (byte*)src.address;
                    byte* to = (byte*)dst.address;
                    if (src < dst)
                        for (uint i = size - 1; i >= 0; i--)
                            to[i] = from[i];
                    else if (src > dst)
                        for (uint i = 0; i < size; i++)
                            to[i] = from[i];
                    break;
            }
        }

        internal static unsafe void Fill(VoidPtr dest, uint length, byte value)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT: { 
                    Win32.FillMemory(dest, length, value); 
                    break; 
                }
                case PlatformID.MacOSX: { 
                    OSX.memset(dest, value, length); 
                    break; 
                }
                case PlatformID.Unix: { 
                    if (Directory.Exists("/Applications")
                        & Directory.Exists("/System")
                        & Directory.Exists("/Users")
                        & Directory.Exists("/Volumes"))
                        goto case PlatformID.MacOSX;
                    else
                        Linux.memset(dest, value, length); 
                    break;
                    }
                default:
                    byte* to = (byte*)dest.address;
                    for (uint i = 0; i < length; i++)
                        to[i] = value;
                    break;
            }
        }
    }
}
