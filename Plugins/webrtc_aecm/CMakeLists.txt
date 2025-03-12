cmake_minimum_required(VERSION 3.10)
project(WebRTC_AECM VERSION 1.0)

# 设置C++标准
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 设置编译选项
if(MSVC)
  # Windows/MSVC编译选项
  add_compile_options(/W4)
  add_definitions(-D_CRT_SECURE_NO_WARNINGS)
else()
  # GCC/Clang编译选项
  add_compile_options(-Wall -Wextra)
  if(CMAKE_COMPILER_IS_GNUCXX)
    # 特定于GCC的选项
    add_compile_options(-Wno-unused-parameter)
  endif()
endif()

# 检测CPU架构和特性
include(CheckIncludeFile)
include(CheckSymbolExists)

# 平台检测和定义
if(APPLE)
  add_definitions(-DWEBRTC_MAC)
  if(IOS)
    add_definitions(-DWEBRTC_IOS)
  endif()
elseif(UNIX AND NOT APPLE)
  add_definitions(-DWEBRTC_LINUX)
  if(CHROMEOS)
    add_definitions(-DCHROMEOS)
  endif()
elseif(WIN32)
  add_definitions(-DWEBRTC_WIN)
endif()

if(ANDROID)
  add_definitions(-DWEBRTC_LINUX -DWEBRTC_ANDROID)
  if(BUILD_WITH_MOZILLA)
    add_definitions(-DWEBRTC_ANDROID_OPENSLES)
  endif()
endif()

# CPU架构检测和定义
if(CMAKE_SYSTEM_PROCESSOR MATCHES "^aarch64" OR CMAKE_SYSTEM_PROCESSOR MATCHES "^arm64")
  add_definitions(-DWEBRTC_ARCH_ARM64 -DWEBRTC_HAS_NEON)
  set(HAS_NEON, 1)
elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "^arm")
  add_definitions(-DWEBRTC_ARCH_ARM)
  if(ARM_VERSION GREATER_EQUAL 7)
    add_definitions(-DWEBRTC_ARCH_ARM_V7)
    if(ARM_USE_NEON)
      add_definitions(-DWEBRTC_HAS_NEON)
      set(HAS_NEON, 1)
    endif()
  endif()
endif()

# 设置包含目录
include_directories(${CMAKE_CURRENT_SOURCE_DIR})

# 收集所有源文件
set(AECM_SOURCES
  # AECM核心文件
  aecm/aecm_core.cc
  aecm/aecm_core_c.cc
  aecm/echo_control_mobile.cc
  
  # 根据架构添加特定实现
  $<$<BOOL:${HAS_NEON}>:aecm/aecm_core_neon.cc>

  # 延迟估计器
  aecm/delay_estimator.cc
  aecm/delay_estimator_wrapper.cc
  
  # 信号处理
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
  
  # 系统包装器
  aecm/cpu_features.cc
  
  # 其他依赖
  aecm/dot_product_with_scale.cc
  aecm/vector_scaling_operations.c
  aecm/checks.cc
  aecm_export.cc
)

# 根据平台选择spl_sqrt_floor实现
if(CMAKE_SYSTEM_PROCESSOR MATCHES "^arm" OR CMAKE_SYSTEM_PROCESSOR MATCHES "^aarch64")
  list(APPEND AECM_SOURCES aecm/spl_sqrt_floor_arm.S)
else()
  list(APPEND AECM_SOURCES aecm/spl_sqrt_floor.c)
endif()

file(GLOB AECM_HEADERS
  ${CMAKE_CURRENT_LIST_DIR}/aecm/*.h
  acem_export.h
)

# 创建动态库
add_library(webrtc_aecm SHARED ${AECM_SOURCES} ${AECM_HEADERS})
# add_executable(webrtc_aecm main.cc ${AECM_SOURCES} ${AECM_HEADERS})

# 设置库的属性
set_target_properties(webrtc_aecm PROPERTIES
  VERSION ${PROJECT_VERSION}
  SOVERSION 1
  PUBLIC_HEADER "echo_control_mobile.h"
  WINDOWS_EXPORT_ALL_SYMBOLS ON
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