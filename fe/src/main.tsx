import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app/App';
import { installNumericInputGuard } from '@/utils/numericInputGuard';
import '@/styles/global.css';

installNumericInputGuard();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
