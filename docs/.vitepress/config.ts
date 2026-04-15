import { defineConfig } from 'vitepress';
import { withMermaid } from 'vitepress-plugin-mermaid';

const zhNav = [
  { text: '简介', link: '/zh-CN/guide/what-is' },
  { text: '快速开始', link: '/zh-CN/guide/quick-start' },
  { text: '集成指南', link: '/zh-CN/integration/' },
  { text: '高级', link: '/zh-CN/advanced/custom-business-handler' },
  { text: 'English', link: '/en/guide/what-is' },
];

const enNav = [
  { text: 'Overview', link: '/en/guide/what-is' },
  { text: 'Quick Start', link: '/en/guide/quick-start' },
  { text: 'Integration Guide', link: '/en/integration/' },
  { text: 'Advanced', link: '/en/advanced/custom-business-handler' },
  { text: '中文', link: '/zh-CN/guide/what-is' },
];

const zhSidebar = [
  {
    text: '开始',
    items: [
      { text: '简介', link: '/zh-CN/guide/what-is' },
      { text: '快速开始', link: '/zh-CN/guide/quick-start' },
    ],
  },
  {
    text: '集成',
    items: [
      { text: '集成指南', link: '/zh-CN/integration/' },
      { text: 'C# API 使用指南', link: '/zh-CN/integration/avalonia' },
    ],
  },
  {
    text: '高级',
    items: [
      { text: '自定义 Business Handler', link: '/zh-CN/advanced/custom-business-handler' },
      { text: '项目架构', link: '/zh-CN/advanced/architecture' },
    ],
  },
];

const enSidebar = [
  {
    text: 'Start',
    items: [
      { text: 'Overview', link: '/en/guide/what-is' },
      { text: 'Quick Start', link: '/en/guide/quick-start' },
    ],
  },
  {
    text: 'Integration',
    items: [
      { text: 'Integration Guide', link: '/en/integration/' },
      { text: 'C# API Usage Guide', link: '/en/integration/avalonia' },
    ],
  },
  {
    text: 'Advanced',
    items: [
      { text: 'Custom Business Handler', link: '/en/advanced/custom-business-handler' },
      { text: 'Architecture', link: '/en/advanced/architecture' },
    ],
  },
];

export default withMermaid(defineConfig({
  base:
    process.env.GITHUB_ACTIONS === 'true' &&
    process.env.GITHUB_REPOSITORY &&
    !process.env.GITHUB_REPOSITORY.endsWith('.github.io')
      ? `/${process.env.GITHUB_REPOSITORY.split('/')[1]}/`
      : '/',
  title: 'Blender Avalonia Bridge',
  description: 'Windows-first bridge toolkit for embedding Avalonia UI inside Blender.',
  cleanUrls: true,
  lastUpdated: true,
  mermaid: {},
  themeConfig: {
    siteTitle: false,
    search: {
      provider: 'local',
    },
    sidebar: {
      '/zh-CN/': zhSidebar,
      '/en/': enSidebar,
    },
  },
  locales: {
    '/zh-CN/': {
      lang: 'zh-CN',
      label: '简体中文',
      title: 'Blender Avalonia Bridge',
      description: '在 Blender 中嵌入 Avalonia UI 的桥接工具套件。',
      themeConfig: {
        nav: zhNav,
        outline: {
          label: '本页目录',
        },
        docFooter: {
          prev: '上一页',
          next: '下一页',
        },
      },
    },
    '/en/': {
      lang: 'en-US',
      label: 'English',
      title: 'Blender Avalonia Bridge',
      description: 'A bridge toolkit for embedding Avalonia UI inside Blender.',
      themeConfig: {
        nav: enNav,
        outline: {
          label: 'On this page',
        },
        docFooter: {
          prev: 'Previous page',
          next: 'Next page',
        },
      },
    },
  },
}));
