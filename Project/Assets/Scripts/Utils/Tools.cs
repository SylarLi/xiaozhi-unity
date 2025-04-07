using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XiaoZhi.Unity
{
    public static class Tools
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArraySegment<T> EnsureArray<T>(ref T[] array, int length)
        {
            if (array == null)
                array = new T[Mathf.NextPowerOfTwo(length)];
            else if (array.Length < length)
                Array.Resize(ref array, Mathf.NextPowerOfTwo(length));
            return new ArraySegment<T>(array, 0, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> EnsureMemory<T>(ref Memory<T> memory, int length)
        {
            if (memory.Length < length)
            {
                var newMem = new T[Mathf.NextPowerOfTwo(length)];
                memory.CopyTo(newMem);
                memory = newMem;
            }

            return memory[..length].Span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> EnsureSpan<T>(ref Span<T> span, int length)
        {
            if (span.Length < length)
            {
                var newSpan = new T[Mathf.NextPowerOfTwo(length)];
                span.CopyTo(newSpan);
                span = newSpan;
            }

            return span[..length];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Repeat(int value, int length)
        {
            if (length == 0) return value;
            value %= length;
            if (value < 0) value += length;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PCM16Short2Float(ReadOnlySpan<short> from, Span<float> to)
        {
            for (var i = from.Length - 1; i >= 0; i--)
                to[i] = (float)from[i] / short.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PCM16Float2Short(ReadOnlySpan<float> from, Span<short> to)
        {
            for (var i = from.Length - 1; i >= 0; i--)
                to[i] = (short)(from[i] * short.MaxValue);
        }

        public static void EnsureChildren(Transform tr, int length)
        {
            for (var i = tr.childCount - 1; i >= length; i--)
                tr.GetChild(i).gameObject.SetActive(false);
            var child = tr.GetChild(0);
            for (var i = tr.childCount; i < length; i++)
                Object.Instantiate(child, tr);
        }

        public static bool IsValidUrl(string url)
        {
            return Uri.IsWellFormedUriString(url, UriKind.Absolute);
        }

        public static bool IsValidMacAddress(string macAddress)
        {
            const string pattern = @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$";
            return Regex.IsMatch(macAddress, pattern);
        }

        public static float Linear2dB(float linear)
        {
            return (Mathf.Clamp(Mathf.Log10(linear) * 20.0f, -80.0f, 0.0f) + 80) / 80;
        }

        public static unsafe int Trim(Span<short> span, int ext = 0)
        {
            var len = span.Length;
            var start = 0;
            var end = len - 1;
            fixed (short* ptr = span)
            {
                for (; start < len; start++)
                    if (*(ptr + start) < -ext || *(ptr + start) > ext)
                        break;
                if (start == len)
                    return 0;
                for (; end >= start - 1; end--)
                    if (*(ptr + end) < -ext || *(ptr + end) > ext)
                        break;
            }

            var tempLen = end - start + 1;
            if (tempLen <= 0) return 0;
            var temp = span.Slice(start, tempLen);
            var pool = ArrayPool<short>.Shared;
            var buffer = pool.Rent(Mathf.NextPowerOfTwo(tempLen));
            temp.CopyTo(buffer);
            buffer.AsSpan(0, tempLen).CopyTo(span);
            pool.Return(buffer);
            return tempLen;
        }

        public static unsafe int CountZeroes(Span<short> span)
        {
            var count = 0;
            var len = span.Length;
            fixed (short* ptr = span)
            {
                for (var i = 0; i < len; i++)
                    if (*(ptr + i) == 0)
                        count++;
            }

            return count;
        }
    }
}