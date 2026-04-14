---
layout: false
title: Blender Avalonia Bridge Docs
---

<script setup>
import { onMounted } from 'vue'
import { withBase } from 'vitepress'
import { resolveLocalePath } from './.vitepress/theme/localeRedirect.js'

onMounted(() => {
  const target = withBase(resolveLocalePath(typeof navigator === 'undefined' ? undefined : navigator.language))
  window.location.replace(target)
})
</script>

# Redirecting

Redirecting to the right language version of the docs.

- [简体中文](/zh-CN/guide/what-is)
- [English](/en/guide/what-is)

If redirection does not happen automatically, choose a language above.
