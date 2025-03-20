#include <string.h>

#include "aecm_export.h"
#include "aecm/echo_control_mobile.h"

using namespace webrtc;

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

int32_t AECM_BufferFarend(void* aecmInst, const int16_t* farend, size_t nrOfSamples) {
    return WebRtcAecm_BufferFarend(aecmInst, farend, nrOfSamples);
}

int32_t AECM_Process(void* aecmInst, const int16_t* nearendNoisy, const int16_t* nearendClean,
                     int16_t* out, size_t nrOfSamples, int16_t msInSndCardBuf) {
    return WebRtcAecm_Process(aecmInst, nearendNoisy, nearendClean, out, nrOfSamples, msInSndCardBuf);
}

void AECM_Free(void* aecmInst) {
    WebRtcAecm_Free(aecmInst);
}