// SPDX-License-Identifier: MIT

#include <CoreFoundation/CoreFoundation.h>
#include <Foundation/Foundation.h>
#include <IOSurface/IOSurface.h>
#include <Metal/Metal.h>
#include <dlfcn.h>

#include <algorithm>
#include <cstdint>
#include <cstring>
#include <cwchar>
#include <sstream>
#include <string>
#include <vector>

namespace {

#define ABN_EXPORT extern "C" __attribute__((visibility("default")))

struct PyObjectHeadCompat {
  intptr_t refcount;
  void *type;
};

struct BPyGPUTextureCompat {
  PyObjectHeadCompat head;
  void *tex;
};

using GPUTextureWidthFn = int (*)(const void *texture);
using GPUTextureHeightFn = int (*)(const void *texture);
using GPUTextureFormatFn = int (*)(const void *texture);
using GPUTextureFormatNameFn = const char *(*)(int texture_format);
using MTLTextureGetMetalHandleFn = id<MTLTexture> (*)(void *texture);

struct ResolverState {
  bool attempted = false;
  bool available = false;
  std::wstring blender_path;
  std::string message = "not initialized";
  GPUTextureWidthFn texture_width = nullptr;
  GPUTextureHeightFn texture_height = nullptr;
  GPUTextureFormatFn texture_format = nullptr;
  GPUTextureFormatNameFn texture_format_name = nullptr;
  MTLTextureGetMetalHandleFn get_metal_handle = nullptr;
};

ResolverState g_resolver;
uint64_t g_copy_calls = 0;
uint64_t g_copy_failures = 0;
uint32_t g_last_surface_id = 0;
std::vector<std::string> g_symbol_matches;

std::string narrow(const std::wstring &value)
{
  if (value.empty()) {
    return {};
  }
  std::mbstate_t state = {};
  const wchar_t *src = value.c_str();
  const size_t needed = std::wcsrtombs(nullptr, &src, 0, &state);
  if (needed == static_cast<size_t>(-1)) {
    std::string fallback;
    fallback.reserve(value.size());
    for (const wchar_t ch : value) {
      fallback += ch >= 0 && ch <= 0x7f ? char(ch) : '?';
    }
    return fallback;
  }
  std::string result(needed, '\0');
  state = {};
  src = value.c_str();
  std::wcsrtombs(result.data(), &src, result.size() + 1, &state);
  return result;
}

void append_json_string(std::ostringstream &json, const std::string &value)
{
  json << "\"";
  for (const char ch : value) {
    if (ch == '"' || ch == '\\') {
      json << '\\';
    }
    else if (ch == '\n') {
      json << "\\n";
      continue;
    }
    json << ch;
  }
  json << "\"";
}

void write_json(const std::string &payload, char *buffer, const uint32_t buffer_size)
{
  if (buffer == nullptr || buffer_size == 0) {
    return;
  }
  const uint32_t copy_size = std::min<uint32_t>(uint32_t(payload.size()), buffer_size - 1);
  memcpy(buffer, payload.data(), copy_size);
  buffer[copy_size] = '\0';
}

void *find_symbol(const std::initializer_list<const char *> names)
{
  for (const char *name : names) {
    void *address = dlsym(RTLD_DEFAULT, name);
    if (address == nullptr) {
      continue;
    }
    if (g_symbol_matches.size() < 16) {
      std::ostringstream item;
      item << name << "=0x" << std::hex << reinterpret_cast<uint64_t>(address);
      g_symbol_matches.push_back(item.str());
    }
    return address;
  }
  return nullptr;
}

bool resolve_blender_gpu_symbols()
{
  if (g_resolver.attempted) {
    return g_resolver.available;
  }
  g_resolver.attempted = true;
  g_symbol_matches.clear();

  g_resolver.texture_width = reinterpret_cast<GPUTextureWidthFn>(find_symbol({
      "_ZN7blender17GPU_texture_widthEPKNS_3gpu7TextureE",
      "_ZN7blender17GPU_texture_widthEPK10GPUTexture",
      "GPU_texture_width",
  }));
  g_resolver.texture_height = reinterpret_cast<GPUTextureHeightFn>(find_symbol({
      "_ZN7blender18GPU_texture_heightEPKNS_3gpu7TextureE",
      "_ZN7blender18GPU_texture_heightEPK10GPUTexture",
      "GPU_texture_height",
  }));
  g_resolver.texture_format = reinterpret_cast<GPUTextureFormatFn>(find_symbol({
      "_ZN7blender18GPU_texture_formatEPKNS_3gpu7TextureE",
      "_ZN7blender18GPU_texture_formatEPK10GPUTexture",
      "GPU_texture_format",
  }));
  g_resolver.texture_format_name = reinterpret_cast<GPUTextureFormatNameFn>(find_symbol({
      "_ZN7blender23GPU_texture_format_nameENS_3gpu13TextureFormatE",
      "_ZN7blender23GPU_texture_format_nameE16eGPUTextureFormat",
      "GPU_texture_format_name",
  }));
  g_resolver.get_metal_handle = reinterpret_cast<MTLTextureGetMetalHandleFn>(find_symbol({
      "_ZN7blender3gpu10MTLTexture16get_metal_handleEv",
  }));

  g_resolver.available = g_resolver.texture_width != nullptr &&
                         g_resolver.texture_height != nullptr &&
                         g_resolver.texture_format != nullptr &&
                         g_resolver.texture_format_name != nullptr &&
                         g_resolver.get_metal_handle != nullptr;
  g_resolver.message = g_resolver.available
      ? "ready: Blender Metal GPUTexture IOSurface copy"
      : "Blender Metal GPU texture symbols not found";
  return g_resolver.available;
}

BPyGPUTextureCompat *as_python_texture(const uint64_t py_texture)
{
  if (py_texture == 0) {
    g_resolver.message = "GPUTexture PyObject pointer is null";
    return nullptr;
  }
  auto *texture = reinterpret_cast<BPyGPUTextureCompat *>(py_texture);
  if (texture->tex == nullptr) {
    g_resolver.message = "GPUTexture has no native texture pointer";
    return nullptr;
  }
  return texture;
}

bool copy_iosurface_to_texture(const uint32_t surface_id,
                               const uint64_t target_py,
                               const int expected_width,
                               const int expected_height)
{
  @autoreleasepool {
    @try {
      if (!resolve_blender_gpu_symbols()) {
        return false;
      }

      auto *target_py_texture = as_python_texture(target_py);
      if (target_py_texture == nullptr) {
        return false;
      }

      const int target_width = g_resolver.texture_width(target_py_texture->tex);
      const int target_height = g_resolver.texture_height(target_py_texture->tex);
      if (target_width <= 0 || target_height <= 0) {
        g_resolver.message = "Blender target texture has invalid dimensions";
        return false;
      }
      if ((expected_width > 0 && target_width != expected_width) ||
          (expected_height > 0 && target_height != expected_height))
      {
        g_resolver.message = "Blender target texture size does not match IOSurface frame";
        return false;
      }

      id<MTLTexture> target_texture = g_resolver.get_metal_handle(target_py_texture->tex);
      if (target_texture == nil) {
        g_resolver.message = "Blender target texture has no Metal handle";
        return false;
      }

      IOSurfaceRef surface = IOSurfaceLookup(surface_id);
      if (surface == nullptr) {
        g_resolver.message = "IOSurfaceLookup failed; the source frame may have expired";
        return false;
      }

      id<MTLTexture> source_texture = nil;
      id<MTLCommandQueue> queue = nil;
      @try {
        const int surface_width = int(IOSurfaceGetWidth(surface));
        const int surface_height = int(IOSurfaceGetHeight(surface));
        if (surface_width <= 0 || surface_height <= 0) {
          g_resolver.message = "IOSurface has invalid dimensions";
          return false;
        }
        if (surface_width != target_width || surface_height != target_height) {
          g_resolver.message = "IOSurface size does not match Blender target texture";
          return false;
        }

        id<MTLDevice> device = [target_texture device];
        if (device == nil) {
          g_resolver.message = "Blender target texture has no Metal device";
          return false;
        }

        MTLTextureDescriptor *descriptor =
            [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatBGRA8Unorm
                                                               width:surface_width
                                                              height:surface_height
                                                           mipmapped:NO];
        descriptor.usage = MTLTextureUsageShaderRead | MTLTextureUsageRenderTarget;
        source_texture = [device newTextureWithDescriptor:descriptor iosurface:surface plane:0];
        if (source_texture == nil) {
          g_resolver.message = "Could not create Metal texture from IOSurface";
          return false;
        }

        queue = [device newCommandQueue];
        if (queue == nil) {
          g_resolver.message = "Could not create Metal command queue";
          return false;
        }

        id<MTLCommandBuffer> command_buffer = [queue commandBuffer];
        id<MTLBlitCommandEncoder> blit = [command_buffer blitCommandEncoder];
        const MTLSize size = MTLSizeMake(NSUInteger(surface_width), NSUInteger(surface_height), 1);
        [blit copyFromTexture:source_texture
                  sourceSlice:0
                  sourceLevel:0
                 sourceOrigin:MTLOriginMake(0, 0, 0)
                   sourceSize:size
                    toTexture:target_texture
             destinationSlice:0
             destinationLevel:0
            destinationOrigin:MTLOriginMake(0, 0, 0)];
        [blit endEncoding];
        [command_buffer commit];
        [command_buffer waitUntilCompleted];

        if ([command_buffer status] == MTLCommandBufferStatusError) {
          NSError *error = [command_buffer error];
          g_resolver.message =
              error == nil ? "Metal command buffer failed" : [[error localizedDescription] UTF8String];
          return false;
        }

        g_resolver.message = "ready";
        return true;
      }
      @finally {
        if (source_texture != nil) {
          [source_texture release];
        }
        if (queue != nil) {
          [queue release];
        }
        CFRelease(surface);
      }
    }
    @catch (NSException *exception) {
      NSString *reason = [exception reason] ?: [exception name];
      g_resolver.message = reason == nil ? "Objective-C exception during IOSurface copy" :
                                           [reason UTF8String];
      return false;
    }
  }
}

std::string status_json()
{
  resolve_blender_gpu_symbols();
  std::ostringstream json;
  json << "{\"available\":" << (g_resolver.available ? "true" : "false") << ",\"message\":";
  append_json_string(json, g_resolver.message);
  json << ",\"blender_path\":";
  append_json_string(json, narrow(g_resolver.blender_path));
  json << ",\"texture_width_address\":" << reinterpret_cast<uintptr_t>(g_resolver.texture_width)
       << ",\"texture_height_address\":" << reinterpret_cast<uintptr_t>(g_resolver.texture_height)
       << ",\"texture_format_address\":" << reinterpret_cast<uintptr_t>(g_resolver.texture_format)
       << ",\"texture_format_name_address\":"
       << reinterpret_cast<uintptr_t>(g_resolver.texture_format_name)
       << ",\"get_metal_handle_address\":"
       << reinterpret_cast<uintptr_t>(g_resolver.get_metal_handle)
       << ",\"copy_calls\":" << g_copy_calls
       << ",\"copy_failures\":" << g_copy_failures
       << ",\"last_surface_id\":" << g_last_surface_id;
  if (!g_symbol_matches.empty()) {
    json << ",\"matches\":[";
    for (size_t i = 0; i < g_symbol_matches.size(); ++i) {
      if (i > 0) {
        json << ",";
      }
      append_json_string(json, g_symbol_matches[i]);
    }
    json << "]";
  }
  json << "}";
  return json.str();
}

}  // namespace

ABN_EXPORT int ava_blender_native_install(const wchar_t *blender_path, const wchar_t * /*pdb_path*/)
{
  g_resolver = ResolverState{};
  g_resolver.blender_path = blender_path ? blender_path : L"";
  return resolve_blender_gpu_symbols() ? 1 : 0;
}

ABN_EXPORT int ava_blender_native_available()
{
  return resolve_blender_gpu_symbols() ? 1 : 0;
}

ABN_EXPORT int ava_blender_native_copy_iosurface_to_texture(uint32_t surface_id,
                                                            uint64_t target_texture_py,
                                                            int width,
                                                            int height)
{
  ++g_copy_calls;
  g_last_surface_id = surface_id;
  if (surface_id == 0) {
    g_resolver.message = "IOSurfaceID is zero";
    ++g_copy_failures;
    return 0;
  }
  if (copy_iosurface_to_texture(surface_id, target_texture_py, width, height)) {
    return 1;
  }
  ++g_copy_failures;
  return 0;
}

ABN_EXPORT int ava_blender_native_texture_info(uint64_t texture_py,
                                               int *width,
                                               int *height,
                                               char *format,
                                               uint32_t format_size)
{
  if (!resolve_blender_gpu_symbols()) {
    return 0;
  }
  auto *texture = as_python_texture(texture_py);
  if (texture == nullptr) {
    return 0;
  }
  if (width != nullptr) {
    *width = g_resolver.texture_width(texture->tex);
  }
  if (height != nullptr) {
    *height = g_resolver.texture_height(texture->tex);
  }
  if (format != nullptr && format_size > 0) {
    const int format_value = g_resolver.texture_format(texture->tex);
    const char *name = g_resolver.texture_format_name(format_value);
    if (name == nullptr) {
      name = "";
    }
    const uint32_t copy_size = std::min<uint32_t>(uint32_t(strlen(name)), format_size - 1);
    memcpy(format, name, copy_size);
    format[copy_size] = '\0';
  }
  return 1;
}

ABN_EXPORT uint32_t ava_blender_native_status_json(char *buffer, uint32_t buffer_size)
{
  const std::string payload = status_json();
  write_json(payload, buffer, buffer_size);
  return uint32_t(payload.size() + 1);
}
