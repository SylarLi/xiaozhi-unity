package com.sylar.xiaozhi.unity.androidaec;

import android.media.audiofx.AcousticEchoCanceler;

/**
 * AEC管理类，提供声学回声消除功能的控制接口
 */
public class AECManager {
    private static AECManager instance;
    private boolean isAECEnabled = false;
    private int audioSessionId = -1;
    private AcousticEchoCanceler acousticEchoCanceler;

    private AECManager() {}

    public static synchronized AECManager getInstance() {
        if (instance == null) {
            instance = new AECManager();
        }
        return instance;
    }

    /**
     * 设置音频会话ID
     * @param sessionId 音频会话ID
     */
    public void setAudioSessionId(int sessionId) {
        this.audioSessionId = sessionId;
        // 如果已经创建了AEC实例，先释放它
        if (acousticEchoCanceler != null) {
            acousticEchoCanceler.release();
            acousticEchoCanceler = null;
        }
    }

    /**
     * 启用AEC功能
     * @return 是否成功启用AEC
     */
    public boolean enableAEC() {
        if (!isAECEnabled && audioSessionId != -1) {
            if (!AcousticEchoCanceler.isAvailable()) {
                return false;
            }
            
            if (acousticEchoCanceler == null) {
                acousticEchoCanceler = AcousticEchoCanceler.create(audioSessionId);
                if (acousticEchoCanceler == null) {
                    return false;
                }
            }
            
            acousticEchoCanceler.setEnabled(true);
            isAECEnabled = acousticEchoCanceler.getEnabled();
            return isAECEnabled;
        }
        return false;
    }

    /**
     * 禁用AEC功能
     */
    public void disableAEC() {
        if (isAECEnabled && acousticEchoCanceler != null) {
            acousticEchoCanceler.setEnabled(false);
            isAECEnabled = false;
        }
    }

    /**
     * 获取AEC当前状态
     * @return 如果AEC功能已启用则返回true，否则返回false
     */
    public boolean isAECEnabled() {
        return isAECEnabled;
    }

    /**
     * 释放AEC资源
     */
    public void release() {
        if (acousticEchoCanceler != null) {
            acousticEchoCanceler.release();
            acousticEchoCanceler = null;
        }
        isAECEnabled = false;
        audioSessionId = -1;
    }
}