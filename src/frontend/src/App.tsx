import { type ReactElement } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router';

import { I18nProvider } from '@/shared/i18n/I18nProvider.tsx';

import { appRoutes } from './app/routes.tsx';

const router = createBrowserRouter(appRoutes);

export function App(): ReactElement {
  return (
    <I18nProvider>
      <RouterProvider router={router} />
    </I18nProvider>
  );
}
