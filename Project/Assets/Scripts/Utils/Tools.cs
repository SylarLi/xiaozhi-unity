using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public static class Tools
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureArray<T>(ref T[] array, int length)
        {
            if (array.Length < length) 
                Array.Resize(ref array, Mathf.NextPowerOfTwo(length));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureMemory<T>(ref Memory<T> memory, int length)
        {
            if (memory.Length < length) 
                memory = new T[Mathf.NextPowerOfTwo(length)];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureSpan<T>(ref Span<T> span, int length)
        {
            if (span.Length < length) 
                span = new T[Mathf.NextPowerOfTwo(length)];
        }

        public static int Repeat(int value, int length)
        {
            value %= length;
            if (value < 0) value += length;
            return value;
        }
    }
}