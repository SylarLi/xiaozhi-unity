using System;
using FMOD;

namespace XiaoZhi.Unity
{
    public static class FMODHelper
    {
        public static int WritePCM16(Sound sound, int position, ReadOnlySpan<short> data)
        {
            position <<= 1;
            var writeLen = (uint)data.Length << 1;
            sound.@lock((uint)position, writeLen, out var ptr1, out var ptr2, out var len1, out var len2);
            unsafe
            {
                fixed (short* ptr = data)
                {
                    Buffer.MemoryCopy(ptr, ptr1.ToPointer(), len1, len1);
                    if (len2 > 0) Buffer.MemoryCopy(ptr + len1 / 2, ptr2.ToPointer(), len2, len2);
                }
            }

            sound.unlock(ptr1, ptr2, len1, len2);
            return (int)(len1 + len2) >> 1;
        }

        public static int ReadPCM16(Sound sound, int position, Span<short> data)
        {
            position <<= 1;
            var readLen = data.Length << 1;
            sound.@lock((uint)position, (uint)readLen, out var ptr1, out var ptr2, out var len1, out var len2);
            unsafe
            {
                fixed (short* ptr = data)
                {
                    Buffer.MemoryCopy(ptr1.ToPointer(), ptr, len1, len1);
                    if (len2 > 0) Buffer.MemoryCopy(ptr2.ToPointer(), ptr + len1 / 2, len2, len2);
                }
            }

            sound.unlock(ptr1, ptr2, len1, len2);
            return (int)(len1 + len2) >> 1;
        }
    }
}