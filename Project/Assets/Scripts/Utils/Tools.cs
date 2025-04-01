using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XiaoZhi.Unity
{
    public static class Tools
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArraySegment<T> EnsureArray<T>(ref T[] array, int length)
        {
            if (array.Length < length)
                Array.Resize(ref array, Mathf.NextPowerOfTwo(length));
            return new ArraySegment<T>(array, 0, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> EnsureMemory<T>(ref Memory<T> memory, int length)
        {
            if (memory.Length < length)
                memory = new T[Mathf.NextPowerOfTwo(length)];
            return memory[..length].Span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> EnsureSpan<T>(ref Span<T> span, int length)
        {
            if (span.Length < length)
                span = new T[Mathf.NextPowerOfTwo(length)];
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
    }
}