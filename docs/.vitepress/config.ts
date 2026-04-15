import { defineConfig } from 'vitepress';
import { withMermaid } from 'vitepress-plugin-mermaid';

const zhNav = [];

const enNav = [];

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
    ],
  },
  {
    text: 'API',
    items: [
      { text: 'BlenderApi', link: '/zh-CN/api/' },
      { text: 'RNA', link: '/zh-CN/api/rna' },
      { text: 'Ops', link: '/zh-CN/api/ops' },
      { text: 'Observe', link: '/zh-CN/api/observe' },
      { text: 'Shared Types', link: '/zh-CN/api/types' },
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
    ],
  },
  {
    text: 'API',
    items: [
      { text: 'BlenderApi', link: '/en/api/' },
      { text: 'RNA', link: '/en/api/rna' },
      { text: 'Ops', link: '/en/api/ops' },
      { text: 'Observe', link: '/en/api/observe' },
      { text: 'Shared Types', link: '/en/api/types' },
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
  description: 'Bridge toolkit for embedding Avalonia UI inside Blender on Windows and macOS.',
  appearance: 'dark',
  cleanUrls: true,
  lastUpdated: true,
  mermaid: {},
  themeConfig: {
    siteTitle: false,
    search: {
      provider: 'local',
    },
    socialLinks: [
      { icon: 'github', link: 'https://github.com/atticus-lv/BlenderAvaloniaBridge/' },
    ],
    sidebar: {
      '/zh-CN/': zhSidebar,
      '/en/': enSidebar,
    },
  },
  locales: {
    en: {
      lang: 'en-US',
      label: 'English',
      link: '/en/',
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
    'zh-CN': {
      lang: 'zh-CN',
      label: '简体中文',
      link: '/zh-CN/',
      title: 'Blender Avalonia Bridge',
      description: '一个支持 Windows 和 macOS 的 Blender Avalonia UI 桥接工具套件。',
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
  },
}));
