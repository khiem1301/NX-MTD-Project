export default {
  testEnvironment: 'jsdom',
  testTimeout: 20000,
  transform: {
    // ts-jest biên dịch sang CommonJS nên cần esModuleInterop để default import
    // của các package CJS (dayjs...) chạy được trong test; build Vite không bị ảnh hưởng.
    '^.+\.tsx?$': ['ts-jest', { tsconfig: { esModuleInterop: true } }],
  },
  moduleNameMapper: {
    // Ảnh (logo...) trong test chỉ cần một đường dẫn giả; đặt trước '@/' để không đọc file ảnh thật.
    '\.(png|jpe?g|svg)$': '<rootDir>/src/test/fileStub.ts',
    '^@/(.*)$': '<rootDir>/src/$1',
    '\.(css|scss)$': 'identity-obj-proxy',
  },
  setupFilesAfterEnv: ['<rootDir>/src/test/setupTests.ts'],
};
