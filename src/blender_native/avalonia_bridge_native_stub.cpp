// SPDX-License-Identifier: MIT

#include <algorithm>
#include <cstdint>
#include <cstring>

namespace {

#define ABN_EXPORT extern "C" __attribute__((visibility("default")))

const char *kStatus =
    "{\"available\":false,\"message\":\"Avalonia Bridge native GPU hook is only implemented on "
    "macOS Metal.\"}";

void write_status(char *buffer, const uint32_t buffer_size)
{
  if (buffer == nullptr || buffer_size == 0) {
    return;
  }
  const uint32_t copy_size = std::min<uint32_t>(uint32_t(strlen(kStatus)), buffer_size - 1);
  memcpy(buffer, kStatus, copy_size);
  buffer[copy_size] = '\0';
}

}  // namespace

ABN_EXPORT int ava_blender_native_install(const wchar_t * /*blender_path*/,
                                          const wchar_t * /*pdb_path*/)
{
  return 0;
}

ABN_EXPORT int ava_blender_native_available()
{
  return 0;
}

ABN_EXPORT int ava_blender_native_copy_iosurface_to_texture(uint32_t /*surface_id*/,
                                                            uint64_t /*target_texture_py*/,
                                                            int /*width*/,
                                                            int /*height*/)
{
  return 0;
}

ABN_EXPORT int ava_blender_native_texture_info(uint64_t /*texture_py*/,
                                               int * /*width*/,
                                               int * /*height*/,
                                               char * /*format*/,
                                               uint32_t /*format_size*/)
{
  return 0;
}

ABN_EXPORT uint32_t ava_blender_native_status_json(char *buffer, uint32_t buffer_size)
{
  write_status(buffer, buffer_size);
  return uint32_t(strlen(kStatus) + 1);
}
