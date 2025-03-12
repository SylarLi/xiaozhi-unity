#include <string.h>

#include "aecm_export.h"
#include "aecm/echo_control_mobile.h"

using namespace webrtc;

static const int kMaxSplitFrameLength = 160;

void* AECM_Create() {
    return WebRtcAecm_Create();
}

int32_t AECM_Init(void* aecmInst, int32_t sampFreq) {
    return WebRtcAecm_Init(aecmInst, sampFreq);
}

int32_t AECM_SetConfig(void* aecmInst, int16_t cngMode, int16_t echoMode) {
    AecmConfig config;
    config.cngMode = cngMode;
    config.echoMode = echoMode;
    return WebRtcAecm_set_config(aecmInst, config);
}

int32_t AECM_Process(void* aecmInst, int16_t* nearInput, int16_t* farInput, 
                    size_t inSampleCount, int32_t inSampleRate, int16_t msInSndCardBuf) {
    size_t kInSampleRate = inSampleRate / 100;
    size_t numFrames = kInSampleRate < kMaxSplitFrameLength ? kInSampleRate : kMaxSplitFrameLength;
    int32_t nCount = inSampleCount / numFrames;
    int16_t outBuffer[kMaxSplitFrameLength];
    for (int32_t i = 0; i < nCount; i++) {
        int nRet = WebRtcAecm_BufferFarend(aecmInst, farInput, numFrames);
        if (nRet != 0) {
            return -1;
        }
        nRet = WebRtcAecm_Process(aecmInst, nearInput, NULL, outBuffer, numFrames, msInSndCardBuf);
        if (nRet != 0) {
            return nRet;
        }
        memcpy(nearInput, outBuffer, numFrames * sizeof(int16_t));
        nearInput += numFrames;
        farInput += numFrames;
    }
}

void AECM_Free(void* aecmInst) {
    WebRtcAecm_Free(aecmInst);
}