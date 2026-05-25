cmake_minimum_required(VERSION 3.22)

project(avalonia_bridge_native LANGUAGES CXX)

set(AVALONIA_BRIDGE_NATIVE_OUTPUT_DIR
    "${CMAKE_CURRENT_LIST_DIR}/../blender_extension/avalonia_bridge/native"
    CACHE PATH "Directory where the Blender extension loads the native hook library")

if(APPLE)
  enable_language(OBJCXX)

  add_library(avalonia_bridge_native SHARED
    avalonia_bridge_native_macos.mm
  )
  target_compile_features(avalonia_bridge_native PRIVATE cxx_std_20)
  target_link_libraries(avalonia_bridge_native PRIVATE
    "-framework Foundation"
    "-framework Metal"
    "-framework IOSurface"
    "-framework CoreFoundation"
  )
else()
  add_library(avalonia_bridge_native SHARED
    avalonia_bridge_native_stub.cpp
  )
  target_compile_features(avalonia_bridge_native PRIVATE cxx_std_20)
endif()

set_target_properties(
  avalonia_bridge_native
  PROPERTIES
    PREFIX ""
    OUTPUT_NAME "avalonia_bridge_native"
    RUNTIME_OUTPUT_DIRECTORY "${AVALONIA_BRIDGE_NATIVE_OUTPUT_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${AVALONIA_BRIDGE_NATIVE_OUTPUT_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${AVALONIA_BRIDGE_NATIVE_OUTPUT_DIR}"
    LIBRARY_OUTPUT_DIRECTORY "${AVALONIA_BRIDGE_NATIVE_OUTPUT_DIR}"
    LIBRARY_OUTPUT_DIRECTORY_RELEASE "${AVALONIA_BRIDGE_NATIVE_OUTPUT_DIR}"
    LIBRARY_OUTPUT_DIRECTORY_DEBUG "${AVALONIA_BRIDGE_NATIVE_OUTPUT_DIR}"
)
