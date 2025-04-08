plugins {
    alias(libs.plugins.android.library)
}

android {
    signingConfigs {
        getByName("debug") {
            storeFile = file("D:\\Github\\xiaozhi-unity\\Project\\user.keystore")
            storePassword = "123456"
            keyAlias = "xiaozhi.unity"
            keyPassword = "123456"
        }
        create("release") {
            storeFile = file("D:\\Github\\xiaozhi-unity\\Project\\user.keystore")
            storePassword = "123456"
            keyAlias = "xiaozhi.unity"
            keyPassword = "123456"
        }
    }
    namespace = "com.sylar.xiaozhi.unity.lib"
    compileSdk = 35

    defaultConfig {
        minSdk = 22

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        consumerProguardFiles("consumer-rules.pro")
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
            signingConfig = signingConfigs.getByName("release")
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
}

dependencies {

    implementation(libs.appcompat)
    implementation(libs.material)
    testImplementation(libs.junit)
    androidTestImplementation(libs.ext.junit)
    androidTestImplementation(libs.espresso.core)
}