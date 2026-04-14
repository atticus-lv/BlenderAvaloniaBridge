import test from 'node:test';
import assert from 'node:assert/strict';

import { resolveLocalePath } from '../.vitepress/theme/localeRedirect.js';

test('defaults to English when locale is missing', () => {
    assert.equal(resolveLocalePath(), '/en/guide/what-is');
});

test('routes English locales to the English docs', () => {
    assert.equal(resolveLocalePath('en-US'), '/en/guide/what-is');
    assert.equal(resolveLocalePath('en'), '/en/guide/what-is');
});

test('routes Chinese locales to the Chinese docs', () => {
    assert.equal(resolveLocalePath('zh-CN'), '/zh-CN/guide/what-is');
    assert.equal(resolveLocalePath('zh-Hans'), '/zh-CN/guide/what-is');
});

test('falls back to English for unsupported locales', () => {
    assert.equal(resolveLocalePath('ja-JP'), '/en/guide/what-is');
});
