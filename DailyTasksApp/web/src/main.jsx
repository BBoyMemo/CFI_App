import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import './i18n';
import './index.css';
import App from './App';
import { AuthProvider } from './auth/AuthContext';

createRoot(document.getElementById('root')).render(
  <StrictMode>
    {/* Same prefix as Vite's `base`: "/dailytasks" in production, "/" in dev. */}
    <BrowserRouter basename={import.meta.env.BASE_URL.replace(/\/$/, '') || '/'}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
);
