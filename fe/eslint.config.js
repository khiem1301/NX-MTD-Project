import js from '@eslint/js';
import globals from 'globals';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';

export default tseslint.config(
  { ignores: ['dist', 'coverage', 'node_modules'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    languageOptions: {
      ecmaVersion: 'latest',
      globals: { ...globals.browser },
    },
    plugins: { 'react-hooks': reactHooks, 'react-refresh': reactRefresh },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unused-vars': [
        'warn',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      '@typescript-eslint/consistent-type-imports': 'warn',
      'no-console': ['warn', { allow: ['warn', 'error'] }],
      eqeqeq: ['error', 'always'],
      // Thông báo, cảnh báo, xác nhận dùng antd (message, Alert, Modal, ConfirmAction), không dùng hộp thoại gốc của trình duyệt.
      'no-alert': 'error',
      'no-restricted-properties': [
        'error',
        {
          object: 'window',
          property: 'confirm',
          message: 'Dùng ConfirmAction hoặc Modal của antd.',
        },
        {
          object: 'window',
          property: 'alert',
          message: 'Dùng message, notification hoặc Alert của antd.',
        },
        { object: 'window', property: 'prompt', message: 'Dùng Modal có ô nhập của antd.' },
      ],
    },
  },
  {
    files: ['**/*.test.{ts,tsx}'],
    languageOptions: { globals: { ...globals.jest } },
  },
);
