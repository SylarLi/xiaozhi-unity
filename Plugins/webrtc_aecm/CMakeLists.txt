cmake_minimum_required(VERSION 3.10)
project(WebRTC_AECM VERSION 1.0)

# 设置C++标准
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 设置编译优化选项
if(MSVC)
  # Windows/MSVC编译选项
  add_compile_options(/W4 /O2 /Oi /GL)
  add_link_options(/LTCG)
  add_definitions(-D_CRT_SECURE_NO_WARNINGS)
else()
  # GCC/Clang编译选项
  add_compile_options(-Wall -Wextra -O3 -ffast-math -fomit-frame-pointer)
  if(CMAKE_COMPILER_IS_GNUCXX)
    add_compile_options(-flto)
    add_link_options(-flto)
  endif()
endif()

if(ANDROID)
  # 根据不同架构设置优化选项
  if(ANDROID_ABI STREQUAL "arm64-v8a")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -march=armv8-a")
  elseif(ANDROID_ABI STREQUAL "armeabi-v7a")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -march=armv7-a -mfpu=neon")
  endif()
endif()

# ... existing code ...

# 平台检测和定义
if(WIN32)
  add_definitions(-DWEBRTC_WIN)
elseif(ANDROID)
  add_definitions(-DWEBRTC_LINUX -DWEBRTC_ANDROID -DWEBRTC_POSIX)
elseif(APPLE)
  add_definitions(-DWEBRTC_MAC -DWEBRTC_POSIX)
  if(IOS)
    add_definitions(-DWEBRTC_IOS)
  endif()
elseif(UNIX)
  add_definitions(-DWEBRTC_LINUX -DWEBRTC_POSIX)
endif()

# CPU架构检测和定义
if(CMAKE_SYSTEM_PROCESSOR MATCHES "^aarch64" OR CMAKE_SYSTEM_PROCESSOR MATCHES "^arm64")
  add_definitions(-DWEBRTC_ARCH_ARM64 -DWEBRTC_HAS_NEON)
  option(HAS_NEON "has neon" ON)
elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "^arm")
  add_definitions(-DWEBRTC_ARCH_ARM)
  if(ARM_VERSION GREATER_EQUAL 7)
    add_definitions(-DWEBRTC_ARCH_ARM_V7)
    if(ARM_USE_NEON)
      add_definitions(-DWEBRTC_HAS_NEON)
      option(HAS_NEON "has neon" ON)
    endif()
  endif()
endif()

# 设置包含目录
include_directories(${CMAKE_CURRENT_SOURCE_DIR})

# 收集所有源文件
set(AECM_SOURCES
  aecm/aecm_core.cc
  aecm/aecm_core_c.cc
  aecm/echo_control_mobile.cc
  aecm/delay_estimator.cc
  aecm/delay_estimator_wrapper.cc
  aecm/complex_bit_reverse.c
  aecm/complex_fft.c
  aecm/cross_correlation.c
  aecm/division_operations.c
  aecm/energy.c
  aecm/get_scaling_square.c
  aecm/min_max_operations.c
  aecm/real_fft.c
  aecm/ring_buffer.c
  aecm/spl_init.c
  aecm/spl_inl.c
  aecm/randomization_functions.c
  aecm/downsample_fast.c
  aecm/cpu_features.cc
  aecm/dot_product_with_scale.cc
  aecm/vector_scaling_operations.c
  aecm/checks.cc
  aecm_export.cc
  aecm/spl_sqrt_floor.c
)

if(HAS_NEON)
  list(APPEND AECM_SOURCES
    aecm/aecm_core_neon.cc
    aecm/min_max_operations_neon.c
    aecm/cross_correlation_neon.c
    aecm/downsample_fast_neon.c
  )
endif()

# 创建动态库
add_library(webrtc_aecm SHARED ${AECM_SOURCES})

# 设置库的属性
set_target_properties(webrtc_aecm PROPERTIES
  VERSION ${PROJECT_VERSION}
  PUBLIC_HEADER "aecm_export.h"
)

# 添加导出宏定义
target_compile_definitions(webrtc_aecm
  PRIVATE WEBRTC_DLL
  PUBLIC WEBRTC_DLL_EXPORT
)

# 安装规则
include(GNUInstallDirs)
install(TARGETS webrtc_aecm
  LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
  ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
  PUBLIC_HEADER DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}/webrtc_aecm
)

# 配置包版本文件
include(CMakePackageConfigHelpers)
write_basic_package_version_file(
  "${CMAKE_CURRENT_BINARY_DIR}/WebRTC_AECMConfigVersion.cmake"
  VERSION ${PROJECT_VERSION}
  COMPATIBILITY SameMajorVersion
)

# 输出信息
message(STATUS "WebRTC AECM module configured successfully")
message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")