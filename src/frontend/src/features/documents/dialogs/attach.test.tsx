import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';

import { AttachDocumentDialog } from '@/features/documents/dialogs/AttachDocumentDialog.tsx';
import { translate } from '@/shared/i18n/i18n.ts';
import { I18nProvider } from '@/shared/i18n/I18nProvider.tsx';
import { documentSummary, PROJECT_ID } from '@/test/documentFixtures.ts';
import { mockApi, page } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations } from '@/test/renderApp.tsx';

// MOD-054 Attach, as an owning module mounts it (no WF-12 route; TASK-037 F-8).

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const CLEAN = documentSummary({
  id: 'a0000000-0000-4000-8000-000000000001',
  title: { text: 'Signed acceptance', language: 'EN' },
});
const QUARANTINED = documentSummary({
  id: 'a0000000-0000-4000-8000-000000000002',
  title: { text: 'Infected upload', language: 'EN' },
  latestScanState: 'QUARANTINED',
});
const SCANNING = documentSummary({
  id: 'a0000000-0000-4000-8000-000000000003',
  title: { text: 'Fresh upload', language: 'EN' },
  latestScanState: 'SCAN_PENDING',
});

function openAttach(onAttach = vi.fn(() => Promise.resolve())) {
  const api = mockApi().on('GET', /^\/documents$/, {
    body: page([CLEAN, QUARANTINED, SCANNING]),
  });
  const onDone = vi.fn();
  render(
    <I18nProvider initialLanguage="en">
      <AttachDocumentDialog
        open
        projectId={PROJECT_ID}
        targetLabel="Milestone 3"
        onAttach={onAttach}
        onClose={vi.fn()}
        onDone={onDone}
      />
    </I18nProvider>,
  );
  return { api, onAttach, onDone };
}

describe('MOD-054 Attach', () => {
  test('a quarantined or unscanned document cannot be chosen, and says why', async () => {
    const { api } = openAttach();
    const dialog = screen.getByRole('dialog', { name: en('documents.attach.title') });

    const infected = await within(dialog).findByRole('radio', { name: 'Infected upload' });
    expect((infected as HTMLInputElement).disabled).toBe(true);
    expect(
      document.getElementById(infected.getAttribute('aria-describedby') ?? '')?.textContent,
    ).toBe(en('documents.attach.quarantined'));
    const fresh = within(dialog).getByRole('radio', { name: 'Fresh upload' });
    expect((fresh as HTMLInputElement).disabled).toBe(true);
    expect(document.getElementById(fresh.getAttribute('aria-describedby') ?? '')?.textContent).toBe(
      en('documents.attach.notClean'),
    );
    expect(within(dialog).getByRole('radio', { name: 'Signed acceptance' })).toHaveProperty(
      'disabled',
      false,
    );

    const [request] = api.requestsTo('GET', /^\/documents$/);
    expect(request?.query.get('projectId')).toBe(PROJECT_ID);
    expect(request?.query.get('status')).toBe('ACTIVE');
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('a clean document is handed to the owning module’s attach call', async () => {
    const { onAttach, onDone } = openAttach();
    const user = userEvent.setup();
    const dialog = screen.getByRole('dialog');

    await user.click(await within(dialog).findByRole('radio', { name: 'Signed acceptance' }));
    await user.click(within(dialog).getByRole('button', { name: en('documents.attach.confirm') }));

    expect(onAttach).toHaveBeenCalledWith(CLEAN);
    expect(onDone).toHaveBeenCalledOnce();
  });

  test('nothing chosen is refused before the module is called', async () => {
    const { onAttach } = openAttach();
    const user = userEvent.setup();
    const dialog = screen.getByRole('dialog');
    await within(dialog).findByRole('radio', { name: 'Signed acceptance' });

    await user.click(within(dialog).getByRole('button', { name: en('documents.attach.confirm') }));

    expect(within(dialog).getByText(en('documents.attach.chooseError'))).toBeTruthy();
    expect(onAttach).not.toHaveBeenCalled();
  });
});
