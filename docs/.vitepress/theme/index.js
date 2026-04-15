import './custom.css';
import DefaultTheme from 'vitepress/theme';
import { inBrowser, onContentUpdated } from 'vitepress';

const zoomSelector = '.vp-doc img:not(.no-zoom)';
let zoomInstance;

async function bindImageZoom() {
  if (!inBrowser) {
    return;
  }

  const { default: mediumZoom } = await import('medium-zoom');

  if (!zoomInstance) {
    zoomInstance = mediumZoom({
      background: 'rgba(10, 14, 24, 0.84)',
      margin: 32,
    });
  }

  zoomInstance.detach();
  zoomInstance.attach(document.querySelectorAll(zoomSelector));
}

if (inBrowser) {
  onContentUpdated(() => {
    void bindImageZoom();
  });
}

export default DefaultTheme;
