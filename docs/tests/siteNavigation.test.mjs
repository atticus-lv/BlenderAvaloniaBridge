import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const docsRoot = new URL('../', import.meta.url);

function readDoc(relativePath) {
    return readFileSync(new URL(relativePath, docsRoot), 'utf8');
}

test('root locale redirect sends users directly to the English overview article', () => {
    const index = readDoc('index.md');

    assert.match(index, /\/en\/guide\/what-is/);
});

test('Chinese locale entry redirects to the overview article instead of rendering a home page', () => {
    const index = readDoc('zh-CN/index.md');

    assert.doesNotMatch(index, /layout:\s*home/);
    assert.match(index, /\/zh-CN\/guide\/what-is/);
});

test('English locale entry redirects to the overview article instead of rendering a home page', () => {
    const index = readDoc('en/index.md');

    assert.doesNotMatch(index, /layout:\s*home/);
    assert.match(index, /\/en\/guide\/what-is/);
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
