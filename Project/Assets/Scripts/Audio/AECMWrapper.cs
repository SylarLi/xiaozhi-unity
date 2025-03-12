using System;
using System.Runtime.InteropServices;

namespace XiaoZhi.Audio
{
    public static class AECMWrapper
    {
#if UNITY_IOS
        private const string LibraryName = "__Internal";
#else
        private const string LibraryName = "webrtc_aecm";
#endif

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr AECM_Create();
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int AECM_Init(IntPtr aecmInst, int sampFreq);

        public enum RoutineMode
        {
            QuietEarpieceOrHeadset,
            Earpiece,
            LoudEarpiece,
            Speakerphone,
            LoudSpeakerphone
        }

        /**
         * @param cngMode: 0(false), 1(true)
         * @param echoMode: RoutingMode
         */
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int AECM_SetConfig(IntPtr aecmInst, int cngMode, RoutineMode echoMode = RoutineMode.Speakerphone);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int AECM_Process(IntPtr aecmInst, short* nearInput, short* farInput,
            long inSampleCount, int inSampleRate, int msInSndCardBuf);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void AECM_Free(IntPtr aecmInst);
    }
}