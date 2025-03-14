// using System;
// using FMOD;
//
// namespace XiaoZhi.Unity
// {
//     public static class FMODHelper
//     {
//         private int WriteSoundRepeatly(Sound sound, uint writePosition, Span<short> data)
//         {
//             sound.getLength(out var soundLength, TIMEUNIT.PCMBYTES);
//             sound.@lock(writePosition, (uint)data.Length * 2, out var ptr1, out var ptr2, out var len1, out var len2);
//             unsafe
//             {
//                 fixed (short* ptr = data)
//                 {
//                     Buffer.MemoryCopy(ptr, ptr1.ToPointer(), len1, len1);
//                     Buffer.MemoryCopy(ptr + len1 / 2, ptr2.ToPointer(), len2, len2);
//                 }
//             }
//
//             sound.unlock(ptr1, ptr2, len1, len2);
//         }
//
//         public static int WriteSound(Sound sound, uint writePosition, Span<short> data)
//         {
//             sound.@lock(writePosition, (uint)data.Length * 2, out var ptr1, out var ptr2, out var len1, out var len2);
//             unsafe
//             {
//                 fixed (short* ptr = data)
//                 {
//                     Buffer.MemoryCopy(ptr, ptr1.ToPointer(), len1, len1);
//                     Buffer.MemoryCopy(ptr + len1 / 2, ptr2.ToPointer(), len2, len2);
//                 }
//             }
//
//             sound.unlock(ptr1, ptr2, len1, len2);
//             return len1 + len2;
//         }
//     }
// }