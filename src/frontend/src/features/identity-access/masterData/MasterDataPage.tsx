import { type ReactElement, type SyntheticEvent, useCallback, useState } from 'react';
import { useSearchParams } from 'react-router';

import { ImportAction } from '@/features/data-import/ImportAction.tsx';
import { projectImportApi } from '@/features/projects/api/followUpApi.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';
import { type MasterDataItemSummary } from '@/shared/api/masterData.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge, type StatusTone } from '@/shared/ui/StatusBadge.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { usePersonNames } from '../assignments/useUserNames.ts';
import { type Confirmation, ConfirmDialog } from '../components/ConfirmDialog.tsx';
import { useCan } from '../permissions/permissions.ts';
import { problemMessage } from '../problems.ts';

/** A catalogue as GET /master-data-catalogues lists it (FG-04). */
interface Catalogue {
  id: string;
  code: string;
  name: { ar: string; en: string };
  allowsHierarchy: boolean;
  isSystem: boolean;
  /** The catalogue an item's parent belongs to, when another: CITY in REGION, STRATEGIC_INITIATIVE in STRATEGIC_PORTFOLIO. */
  parentCatalogueId: string | null;
}

type Command = 'validate' | 'publish' | 'retire';

const COMMANDS: readonly Command[] = ['validate', 'publish', 'retire'];

/** Whether an item's state offers the command. */
function allows(item: MasterDataItemSummary, command: Command): boolean {
  switch (command) {
    case 'validate':
      return item.lifecycleState === 'DRAFT';
    case 'publish':
      return item.lifecycleState === 'VALIDATED';
    case 'retire':
      return item.lifecycleState === 'PUBLISHED' && !item.isSystem;
  }
}

/** A command sent with the item's current ETag. */
async function sendCommand(item: MasterDataItemSummary, command: Command): Promise<void> {
  const current = await apiRequest<MasterDataItemSummary>(`/master-data-items/${item.id}`);
  await apiRequest(`/master-data-items/${item.id}/${command}`, {
    method: 'POST',
    body: {},
    ifMatch: current.etag,
  });
}

const STATE_TONES: Record<string, StatusTone> = {
  DRAFT: 'neutral',
  VALIDATED: 'info',
  PUBLISHED: 'positive',
  RETIRED: 'warning',
};

/** The catalogues the 2 Nov 2026 review asks screens for come first; the rest follow by name. */
const FIRST = [
  'STRATEGIC_PORTFOLIO',
  'STRATEGIC_INITIATIVE',
  'PROJECT_CLASSIFICATION',
  'REGION',
  'CITY',
  'PRIORITY',
];

const CODE_PATTERN = /^[A-Z0-9][A-Z0-9_-]{0,49}$/;

async function readCatalogues(signal: AbortSignal): Promise<Catalogue[]> {
  return (await apiRequest<Catalogue[]>('/master-data-catalogues', { signal })).data;
}

async function readItems(
  catalogueId: string,
  signal: AbortSignal,
): Promise<MasterDataItemSummary[]> {
  return (
    await apiRequest<{ items: MasterDataItemSummary[] }>('/master-data-items', {
      query: { catalogueId, pageSize: 200 },
      signal,
    })
  ).data.items;
}

/**
 * Master data configuration (FG-04 ADM-020–029; the 2 Nov 2026 review asks for it for strategic portfolios and initiatives, sectors, regions and
 * cities): a catalogue's items with their code, both labels, parent and state. A new item is a DRAFT; it is validated, then published — by
 * different people (TASK-110) — before a form offers it. A published item is retired, never deleted.
 */
export function MasterDataPage(): ReactElement {
  const { t, language } = useI18n();
  const [params, setParams] = useSearchParams();
  const catalogues = useApiResource(readCatalogues);
  const sorted = (catalogues.data ?? []).toSorted(
    (a, b) =>
      (FIRST.includes(a.code) ? FIRST.indexOf(a.code) : 99) -
        (FIRST.includes(b.code) ? FIRST.indexOf(b.code) : 99) ||
      a.name[language].localeCompare(b.name[language]),
  );
  const chosen = sorted.find((c) => c.code === params.get('catalogue')) ?? sorted[0];
  const parentCatalogue =
    chosen === undefined
      ? undefined
      : chosen.parentCatalogueId === null
        ? chosen.allowsHierarchy
          ? chosen
          : undefined
        : sorted.find((c) => c.id === chosen.parentCatalogueId);

  return (
    <>
      <PageHeader
        title={t('identityAccess.masterData.title')}
        description={t('identityAccess.masterData.description')}
      />
      {catalogues.error !== null && (
        <ErrorState
          error={catalogues.error}
          message={problemMessage(catalogues.error, t)}
          onRetry={catalogues.reload}
        />
      )}
      <RequestedValues />
      {catalogues.loading && <LoadingState />}
      {chosen !== undefined && (
        <div className="master-data">
          {/* The list being looked at, always one: a required choice, so not a filter folded behind the Filters button. */}
          <SelectField
            label={t('identityAccess.masterData.catalogue')}
            name="catalogue"
            value={chosen.code}
            options={sorted.map((c) => ({ value: c.code, label: c.name[language] }))}
            onChange={(value) => {
              setParams({ catalogue: value });
            }}
          />
          <CatalogueItems key={chosen.id} catalogue={chosen} parentCatalogue={parentCatalogue} />
        </div>
      )}
    </>
  );
}

/** One catalogue's items, the commands each state allows, and adding a value. */
function CatalogueItems({
  catalogue,
  parentCatalogue,
}: {
  catalogue: Catalogue;
  parentCatalogue: Catalogue | undefined;
}): ReactElement {
  const { t, language } = useI18n();
  const mayManage = useCan('MASTER_DATA_MANAGE');
  const loadItems = useCallback(
    (signal: AbortSignal) => readItems(catalogue.id, signal),
    [catalogue.id],
  );
  const parentId = parentCatalogue?.id;
  const loadParents = useCallback(
    (signal: AbortSignal) =>
      parentId === undefined ? Promise.resolve([]) : readItems(parentId, signal),
    [parentId],
  );
  const items = useApiResource(loadItems);
  const parents = useApiResource(loadParents);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const selection = useRowSelection();

  const parentName = (id: string | null) =>
    id === null
      ? t('common.values.none')
      : (parents.data?.find((p) => p.id === id)?.label[language] ?? id.slice(0, 8));

  const run = async (item: MasterDataItemSummary, command: Command) => {
    setError(null);
    try {
      await sendCommand(item, command);
      items.reload();
    } catch (caught) {
      setError(problemMessage(caught, t));
    }
  };

  const chosenFor = (command: Command) =>
    (items.data ?? []).filter((item) => selection.selected.has(item.id) && allows(item, command));

  /** The same command on every chosen item that allows it, confirmed by naming them. */
  const confirmMany = (command: Command) => {
    const chosen = chosenFor(command);
    setConfirmation({
      title: t(`identityAccess.masterData.${command}`),
      body: chosen.map((item) => item.code).join('، '),
      confirmLabel: t(`identityAccess.masterData.${command}`),
      destructive: command === 'retire',
      action: async () => {
        for (const item of chosen) {
          await sendCommand(item, command);
        }
        selection.clear();
      },
    });
  };

  return (
    <>
      <div className="form__actions">
        <ImportAction
          collectionPath="/master-data-items"
          registerName={t('identityAccess.masterData.title')}
          onImported={items.reload}
        />
        {mayManage && (
          <button
            type="button"
            className="button button--primary"
            onClick={() => {
              setCreating(true);
            }}
          >
            {t('identityAccess.masterData.create')}
          </button>
        )}
      </div>
      <FormAlert message={error} />
      {items.loading && <LoadingState />}
      {items.error !== null && (
        <ErrorState
          error={items.error}
          message={problemMessage(items.error, t)}
          onRetry={items.reload}
        />
      )}
      {items.data !== undefined &&
        (items.data.length === 0 ? (
          <EmptyState title={t('identityAccess.masterData.empty')} />
        ) : (
          <TableContainer
            caption={catalogue.name[language]}
            selection={{
              selection,
              exportName: `master-data-${catalogue.code.toLowerCase()}`,
              actions: mayManage
                ? COMMANDS.filter((command) => chosenFor(command).length > 0).map((command) => (
                    <button
                      key={command}
                      type="button"
                      className={command === 'retire' ? 'button button--danger' : 'button'}
                      onClick={() => {
                        confirmMany(command);
                      }}
                    >
                      {t(`identityAccess.masterData.${command}`)}
                    </button>
                  ))
                : undefined,
            }}
          >
            <thead>
              <tr>
                <SelectAllHeader ids={items.data.map((item) => item.id)} selection={selection} />
                <th scope="col">{t('identityAccess.masterData.code')}</th>
                <th scope="col">{t('identityAccess.masterData.labelAr')}</th>
                <th scope="col">{t('identityAccess.masterData.labelEn')}</th>
                {parentCatalogue !== undefined && (
                  <th scope="col">{parentCatalogue.name[language]}</th>
                )}
                <th scope="col">{t('identityAccess.masterData.state')}</th>
                {mayManage && <th scope="col">{t('common.table.actions')}</th>}
              </tr>
            </thead>
            <tbody>
              {items.data.map((item) => (
                <tr key={item.id}>
                  <SelectRowCell id={item.id} name={item.code} selection={selection} />
                  <td dir="ltr" className="cell--ltr">
                    {item.code}
                  </td>
                  <td dir="rtl" lang="ar">
                    {item.label.ar}
                  </td>
                  <td dir="ltr" lang="en">
                    {item.label.en}
                  </td>
                  {parentCatalogue !== undefined && <td>{parentName(item.parentItemId)}</td>}
                  <td>
                    <StatusBadge
                      label={t(
                        `identityAccess.masterData.states.${item.lifecycleState as 'DRAFT'}`,
                      )}
                      tone={STATE_TONES[item.lifecycleState] ?? 'neutral'}
                    />
                  </td>
                  {mayManage && (
                    <td>
                      {COMMANDS.filter((command) => allows(item, command)).map((command) => (
                        <button
                          key={command}
                          type="button"
                          className="button button--quiet"
                          onClick={() => void run(item, command)}
                        >
                          {t(`identityAccess.masterData.${command}`)}
                        </button>
                      ))}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </TableContainer>
        ))}
      <ConfirmDialog
        confirmation={confirmation}
        onClose={() => {
          setConfirmation(null);
        }}
        onDone={() => {
          setConfirmation(null);
          items.reload();
        }}
      />
      <CreateItemDialog
        open={creating}
        catalogue={catalogue}
        parentLabel={parentCatalogue?.name[language]}
        parents={(parents.data ?? []).filter((p) => p.lifecycleState !== 'RETIRED')}
        onClose={() => {
          setCreating(false);
        }}
        onCreated={() => {
          setCreating(false);
          items.reload();
        }}
      />
    </>
  );
}

function CreateItemDialog({
  open,
  catalogue,
  parentLabel,
  parents,
  onClose,
  onCreated,
}: {
  open: boolean;
  catalogue: Catalogue;
  parentLabel: string | undefined;
  parents: MasterDataItemSummary[];
  onClose: () => void;
  onCreated: () => void;
}): ReactElement {
  const { t, language } = useI18n();
  const [code, setCode] = useState('');
  const [ar, setAr] = useState('');
  const [en, setEn] = useState('');
  const [parent, setParent] = useState('');
  const [sortOrder, setSortOrder] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const codeValid = CODE_PATTERN.test(code.trim());
  const ready =
    codeValid &&
    ar.trim() !== '' &&
    en.trim() !== '' &&
    (sortOrder === '' || /^[0-9]{1,6}$/.test(sortOrder));

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!ready) {
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await apiRequest('/master-data-items', {
        method: 'POST',
        body: {
          catalogueId: catalogue.id,
          code: code.trim(),
          label: { ar: ar.trim(), en: en.trim() },
          description: null,
          parentItemId: parent === '' ? null : parent,
          sortOrder: sortOrder === '' ? 0 : Number(sortOrder),
        },
      });
      setCode('');
      setAr('');
      setEn('');
      setParent('');
      setSortOrder('');
      onCreated();
    } catch (caught) {
      setError(problemMessage(caught, t));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog
      open={open}
      title={t('identityAccess.masterData.createTitle', { catalogue: catalogue.name[language] })}
      onClose={onClose}
    >
      <form className="form" noValidate onSubmit={(event) => void submit(event)}>
        <FormAlert message={error} />
        <TextField
          label={t('identityAccess.masterData.code')}
          name="code"
          required
          dir="ltr"
          value={code}
          hint={t('identityAccess.codeHint')}
          onChange={setCode}
          error={code !== '' && !codeValid ? t('identityAccess.masterData.codeInvalid') : undefined}
        />
        <TextField
          label={t('identityAccess.masterData.labelAr')}
          name="labelAr"
          required
          dir="rtl"
          value={ar}
          onChange={setAr}
        />
        <TextField
          label={t('identityAccess.masterData.labelEn')}
          name="labelEn"
          required
          dir="ltr"
          value={en}
          onChange={setEn}
        />
        {parentLabel !== undefined && (
          <SelectField
            label={parentLabel}
            name="parentItemId"
            value={parent}
            placeholder={t('common.values.none')}
            options={parents.map((p) => ({
              value: p.id,
              label: `${p.code} — ${p.label[language]}`,
            }))}
            onChange={setParent}
          />
        )}
        <TextField
          label={t('identityAccess.masterData.sortOrder')}
          name="sortOrder"
          dir="ltr"
          inputMode="numeric"
          value={sortOrder}
          onChange={setSortOrder}
        />
        <p className="form__note">{t('identityAccess.masterData.lifecycleNote')}</p>
        <div className="form__actions">
          <button type="submit" className="button button--primary" disabled={!ready || saving}>
            {saving ? t('common.states.saving') : t('identityAccess.masterData.create')}
          </button>
          <button type="button" className="button" disabled={saving} onClick={onClose}>
            {t('common.actions.cancel')}
          </button>
        </div>
      </form>
    </Dialog>
  );
}

/**
 * The values importers asked for (the 2 Nov 2026 review: "an unknown name triggers a request to add it"), oldest first. The administrator adds
 * the value where it belongs — a list here, a department or an entity under Organization — then marks the request added; the importer is
 * notified either way.
 */
function RequestedValues(): ReactElement | null {
  const { t, language, formatDateTime } = useI18n();
  const mayResolve = useCan('MASTER_DATA_MANAGE', 'ORGANIZATION_MANAGE');
  const requests = useApiResource(readOpenRequests);
  const requester = usePersonNames((requests.data ?? []).map((r) => r.requestedBy));
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const selection = useRowSelection();
  if (!mayResolve || requests.data === undefined || requests.data.length === 0) {
    return null;
  }
  const resolve = async (id: string, status: 'ADDED' | 'DECLINED') => {
    setError(null);
    try {
      await projectImportApi.resolveValue(id, status, notes[id] ?? '', language);
      requests.reload();
    } catch (caught) {
      setError(problemMessage(caught, t));
    }
  };
  return (
    <section className="section" aria-labelledby="requested-values-heading">
      <h2 id="requested-values-heading">{t('identityAccess.masterData.requested.title')}</h2>
      <p>{t('identityAccess.masterData.requested.description')}</p>
      <FormAlert message={error} />
      <TableContainer
        caption={t('identityAccess.masterData.requested.title')}
        selection={{ selection, exportName: 'requested-values' }}
      >
        <thead>
          <tr>
            <SelectAllHeader
              ids={requests.data.map((request) => request.id)}
              selection={selection}
            />
            <th scope="col">{t('identityAccess.masterData.requested.list')}</th>
            <th scope="col">{t('identityAccess.masterData.requested.value')}</th>
            <th scope="col">{t('identityAccess.masterData.requested.by')}</th>
            <th scope="col">{t('identityAccess.masterData.requested.note')}</th>
            <th scope="col">{t('common.table.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {requests.data.map((request) => (
            <tr key={request.id}>
              <SelectRowCell id={request.id} name={request.sourceText} selection={selection} />
              <td>{t(`projects.import.dimensionName.${request.dimension}`)}</td>
              <td dir="auto">{request.sourceText}</td>
              <td>
                {requester(request.requestedBy)}
                <span className="cell__aside">{formatDateTime(request.requestedAt)}</span>
              </td>
              <td>
                <TextField
                  label={t('identityAccess.masterData.requested.note')}
                  name={`note-${request.id}`}
                  value={notes[request.id] ?? ''}
                  onChange={(value) => {
                    setNotes((current) => ({ ...current, [request.id]: value }));
                  }}
                />
              </td>
              <td>
                <button
                  type="button"
                  className="button button--quiet"
                  onClick={() => void resolve(request.id, 'ADDED')}
                >
                  {t('identityAccess.masterData.requested.added')}
                </button>
                <button
                  type="button"
                  className="button button--quiet"
                  onClick={() => void resolve(request.id, 'DECLINED')}
                >
                  {t('identityAccess.masterData.requested.declined')}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
    </section>
  );
}

async function readOpenRequests(signal: AbortSignal) {
  return (await projectImportApi.valueRequests('OPEN', signal)).items;
}
