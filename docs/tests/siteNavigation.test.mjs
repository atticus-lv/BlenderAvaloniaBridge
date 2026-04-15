import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const docsRoot = new URL('../', import.meta.url);

function readDoc(relativePath) {
    return readFileSync(new URL(relativePath, docsRoot), 'utf8');
}

test('root docs entry renders a landing page and keeps locale redirect logic', () => {
    const index = readDoc('index.md');

    assert.match(index, /layout:\s*home/);
    assert.match(index, /Practical bridge tooling for Avalonia UI inside Blender/);
    assert.match(index, /link:\s*\/en\//);
    assert.match(index, /link:\s*\/zh-CN\//);
    assert.match(index, /resolveLocalePath/);
    assert.match(index, /window\.location\.replace/);
});

test('Chinese locale entry renders a localized home page', () => {
    const index = readDoc('zh-CN/index.md');

    assert.match(index, /layout:\s*home/);
    assert.match(index, /在 Blender 中使用 Avalonia 构建桌面级 UI/);
    assert.match(index, /\/zh-CN\/guide\/quick-start/);
});

test('English locale entry renders a localized home page', () => {
    const index = readDoc('en/index.md');

    assert.match(index, /layout:\s*home/);
    assert.match(index, /Build desktop-grade UI in Blender with Avalonia/);
    assert.match(index, /What Is Blender Avalonia Bridge/);
    assert.match(index, /\/en\/guide\/quick-start/);
});

test('sidebar configuration does not include locale home entries', () => {
    const config = readDoc('.vitepress/config.ts');

    assert.doesNotMatch(config, /text:\s*'首页',\s*link:\s*'\/zh-CN\/'/);
    assert.doesNotMatch(config, /text:\s*'Home',\s*link:\s*'\/en\/'/);
});

test('site config disables the default site-title home link', () => {
    const config = readDoc('.vitepress/config.ts');

    assert.match(config, /siteTitle:\s*false/);
});

test('theme hides the default navbar title container', () => {
    const themeIndex = readDoc('.vitepress/theme/index.js');
    const themeCss = readDoc('.vitepress/theme/custom.css');

    assert.match(themeIndex, /import '\.\/custom\.css';/);
    assert.match(themeCss, /\.VPNavBarTitle\s*\{/);
    assert.match(themeCss, /display:\s*none/);
});
