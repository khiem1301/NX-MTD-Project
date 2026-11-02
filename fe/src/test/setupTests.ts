import '@testing-library/jest-dom';
import { TextDecoder, TextEncoder } from 'node:util';

Object.assign(globalThis, { TextEncoder, TextDecoder });

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => undefined,
    removeListener: () => undefined,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    dispatchEvent: () => false,
  }),
});

// Ant Design chèn khoảng 300 KB CSS vào trang: jsdom duyệt toàn bộ mỗi lần gọi getComputedStyle (khoảng 200 ms), nên getByRole
// mất vài giây và test vượt thời hạn. Test không phụ thuộc CSS của antd nên thẻ style luôn rỗng.
Object.defineProperty(HTMLStyleElement.prototype, 'innerHTML', {
  configurable: true,
  get: () => '',
  set: () => undefined,
});
