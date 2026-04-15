---
title: Blender Avalonia Bridge Docs
layout: home

hero:
  name: Blender Avalonia Bridge
  text: Practical bridge tooling for Avalonia UI inside Blender
  tagline: Keep transport, input, and business APIs explicit so integration stays understandable.
  actions:
    - theme: brand
      text: English Docs
      link: /en/
    - theme: alt
      text: 中文文档
      link: /zh-CN/

features:
  - title: Headless Or Desktop
    details: Run Avalonia inside Blender with frame transport, or keep a normal desktop window and use Blender only as a business peer.
  - title: BlenderApi Domains
    details: Access Blender from C# through clear domains for RNA, operators, and observation instead of one flat surface.
  - title: Extension Friendly
    details: Keep the default bridge protocol for common flows and add project-specific business endpoints only where needed.
---

<script setup>
import { onMounted } from 'vue'
import { withBase } from 'vitepress'
import { resolveLocalePath } from './.vitepress/theme/localeRedirect.js'

onMounted(() => {
  const locale = typeof navigator === 'undefined' ? undefined : navigator.language
  const target = withBase(resolveLocalePath(locale).replace(/\/guide\/what-is$/, '/'))

  if (window.location.pathname === withBase('/')) {
    window.location.replace(target)
  }
})
</script>
