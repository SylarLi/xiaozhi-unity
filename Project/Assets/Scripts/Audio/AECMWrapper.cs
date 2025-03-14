using System;
using System.Runtime.InteropServices;

namespace XiaoZhi.Audio
{
    public static class AECMWrapper
    {
#if UNITY_IOS
        private const string LibraryName = "__Internal";
#elif UNITY_ANDROID && !UNITY_EDITOR
        private const string LibraryName = "libwebrtc_aecm";
#else
        private const string LibraryName = "webrtc_aecm";
#endif

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr AECM_Create();

        /// <summary>
        /// Initialize the AECM module.
        /// </summary>
        /// <param name="aecmInst">Instance from return of AECM_Create</param>
        /// <param name="sampFreq">Should be 8000 or 16000</param>
        /// <returns></returns>
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
         * @param cngMode: Comfort Noise 0(false), 1(true)
         * @param echoMode: RoutingMode
         */
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int AECM_SetConfig(IntPtr aecmInst, int cngMode = 1,
            RoutineMode echoMode = RoutineMode.Speakerphone);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int AECM_BufferFarend(IntPtr aecmInst, short* farInput, int inSampleCount,
            int inSampleRate);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int AECM_Process(IntPtr aecmInst, short* nearInput, short* cleanInput,
            long inSampleCount, int inSampleRate, int msInSndCardBuf = 100);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void AECM_Free(IntPtr aecmInst);
    }
}