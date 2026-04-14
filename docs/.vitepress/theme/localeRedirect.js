export function resolveLocalePath(locale) {
  if (!locale) {
    return '/en/guide/what-is';
  }

  const normalized = String(locale).toLowerCase();
  if (normalized.startsWith('en')) {
    return '/en/guide/what-is';
  }

  if (normalized.startsWith('zh')) {
    return '/zh-CN/guide/what-is';
  }

  return '/en/guide/what-is';
}
