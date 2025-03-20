#ifndef AECM_EXPORT_H
#define AECM_EXPORT_H

#include <stdint.h>
#include <stddef.h>

#include "aecm/rtc_export.h"

#ifdef __cplusplus
extern "C" {
#endif

RTC_EXPORT void* AECM_Create();

// sampFreq should be 8000 or 16000
RTC_EXPORT int32_t AECM_Init(void* aecmInst, int32_t sampFreq);

// enum RoutingMode {
//     kQuietEarpieceOrHeadset,
//     kEarpiece,
//     kLoudEarpiece,
//     kSpeakerphone,
//     kLoudSpeakerphone
//  };

/**
 * @param cngMode: 0(AECM_FALSE), 1(AECM_TRUE)
 * @param echoMode: RoutingMode --> 0, 1, 2, 3 (default), 4
 */
RTC_EXPORT int32_t AECM_SetConfig(void* aecmInst, int16_t cngMode, int16_t echoMode);

RTC_EXPORT int32_t AECM_BufferFarend(void* aecmInst, const int16_t* farend, size_t nrOfSamples);

RTC_EXPORT int32_t AECM_Process(void* aecmInst, const int16_t* nearendNoisy, const int16_t* nearendClean,
                                int16_t* out, size_t nrOfSamples, int16_t msInSndCardBuf);

RTC_EXPORT void AECM_Free(void* aecmInst);

#ifdef __cplusplus
}
#endif

#endif // AECM_EXPORT_H