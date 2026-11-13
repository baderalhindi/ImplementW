import { type SyntheticEvent, type ReactElement, useCallback, useRef, useState } from 'react';

import { ImportAction } from '@/features/data-import/ImportAction.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { useSearchText } from '@/shared/ui/useSearchText.ts';

import {
  externalEntitiesApi,
  type ExternalEntityTransition,
  usersApi,
} from '../api/identityAccessApi.ts';
import {
  type ExternalEntityDetail,
  type ExternalEntityStatus,
  type ExternalEntitySummary,
} from '../api/types.ts';
import { type Confirmation, ConfirmDialog } from '../components/ConfirmDialog.tsx';
import { type PickedUser, UserPicker } from '../components/UserPicker.tsx';
import { useUserNames } from '../assignments/useUserNames.ts';
import {
  checkText,
  CODE_LENGTH,
  CODE_PATTERN,
  useFocusFirstError,
  useSaveAction,
  UUID_PATTERN,
} from '../forms.ts';
import { labelOf } from '../lookups.ts';
import { problemMessage } from '../problems.ts';

const PAGE_SIZE = 25;
const STATUSES: ExternalEntityStatus[] = ['ACTIVE', 'SUSPENDED', 'RETIRED'];

/** The commands each status allows; RETIRED is terminal (record §3). */
const TRANSITIONS: Record<ExternalEntityStatus, ExternalEntityTransition[]> = {
  ACTIVE: ['suspend', 'retire'],
  SUSPENDED: ['activate', 'retire'],
  RETIRED: [],
};

type Editing = { mode: 'create' } | { mode: 'edit'; entityId: string };

/**
 * ADM-013 Entities (ADR-013): the external organisations whose people may hold per-project grants. Only an ACTIVE
 * entity's people may sign in and act.
 */
export function ExternalEntityListPage(): ReactElement {
  const { t, language } = useI18n();
  const { params, page, setFilter, setFilters, goToPage } = useListParams();
  const [editing, setEditing] = useState<Editing | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const selection = useRowSelection();

  const q = params.get('q') ?? undefined;
  const status = STATUSES.find((s) => s === params.get('status'));

  const load = useCallback(
    (signal: AbortSignal) =>
      externalEntitiesApi.list({ q, status, page, pageSize: PAGE_SIZE }, signal),
    [q, status, page],
  );
  const entities = useApiResource(load, { keepPreviousResult: true });
  const sponsorNames = useUserNames(entities.data?.items.map((e) => e.sponsorUserId) ?? []);

  const [searchText, setSearchText] = useSearchText(q ?? '', (text) => {
    setFilter('q', text, { replace: true });
  });

  const confirm = (entity: ExternalEntitySummary, transition: ExternalEntityTransition) => {
    const name = labelOf(entity.name, language);
    setConfirmation({
      title: t(`identityAccess.entities.transitions.${transition}.title`),
      body: t(`identityAccess.entities.transitions.${transition}.body`, { name }),
      confirmLabel: t(`identityAccess.entities.transitions.${transition}.action`),
      destructive: transition !== 'activate',
      action: () => externalEntitiesApi.transition(entity.id, transition),
    });
  };

  /** The same command on every chosen entity on this page that allows it. */
  const confirmMany = (transition: ExternalEntityTransition) => {
    const chosen = (entities.data?.items ?? []).filter(
      (entity) =>
        selection.selected.has(entity.id) && TRANSITIONS[entity.status].includes(transition),
    );
    setConfirmation({
      title: t(`identityAccess.entities.transitions.${transition}.title`),
      body: chosen.map((entity) => labelOf(entity.name, language)).join('، '),
      confirmLabel: t(`identityAccess.entities.transitions.${transition}.action`),
      destructive: transition !== 'activate',
      action: async () => {
        for (const entity of chosen) {
          await externalEntitiesApi.transition(entity.id, transition);
        }
        selection.clear();
      },
    });
  };
  const chosenAllow = (transition: ExternalEntityTransition) =>
    (entities.data?.items ?? []).some(
      (entity) =>
        selection.selected.has(entity.id) && TRANSITIONS[entity.status].includes(transition),
    );

  return (
    <>
      <PageHeader
        title={t('identityAccess.entities.title')}
        description={t('identityAccess.entities.description')}
        actions={
          <>
            <ImportAction
              collectionPath="/external-entities"
              registerName={t('identityAccess.entities.title')}
              onImported={entities.reload}
            />
            <button
              type="button"
              className="button button--primary"
              onClick={() => {
                setEditing({ mode: 'create' });
              }}
            >
              {t('identityAccess.entities.create')}
            </button>
          </>
        }
      />

      <FilterBar
        onClearAll={() => {
          setFilters({ q: '', status: '' });
        }}
        chips={[
          ...(q === undefined
            ? []
            : [
                {
                  name: 'q',
                  text: t('common.filters.inline', {
                    label: t('identityAccess.entities.search'),
                    value: q,
                  }),
                  onRemove: () => {
                    setFilter('q', '');
                  },
                },
              ]),
          ...(status === undefined
            ? []
            : [
                {
                  name: 'status',
                  text: t('common.filters.inline', {
                    label: t('identityAccess.entities.status'),
                    value: t(`identityAccess.entityStatus.${status}`),
                  }),
                  onRemove: () => {
                    setFilter('status', '');
                  },
                },
              ]),
        ]}
      >
        <form
          className="filters"
          onSubmit={(event) => {
            event.preventDefault();
          }}
        >
          <TextField
            label={t('identityAccess.entities.search')}
            name="q"
            type="search"
            value={searchText}
            onChange={setSearchText}
          />
          <SelectField
            label={t('identityAccess.entities.status')}
            name="status"
            value={status ?? ''}
            placeholder={t('common.filters.any')}
            options={STATUSES.map((value) => ({
              value,
              label: t(`identityAccess.entityStatus.${value}`),
            }))}
            onChange={(value) => {
              setFilter('status', value);
            }}
          />
        </form>
      </FilterBar>

      {entities.loading && entities.data === undefined && <LoadingState />}
      {entities.error !== null && (
        <ErrorState
          error={entities.error}
          message={problemMessage(entities.error, t)}
          onRetry={entities.reload}
        />
      )}
      {entities.data !== undefined &&
        (entities.data.items.length === 0 ? (
          <EmptyState
            title={
              q === undefined && status === undefined
                ? t('identityAccess.entities.empty')
                : t('identityAccess.entities.emptyFiltered')
            }
          />
        ) : (
          <>
            <TableContainer
              caption={t('identityAccess.entities.title')}
              selection={{
                selection,
                exportName: 'external-entities',
                actions: (
                  <>
                    {(['activate', 'suspend', 'retire'] as const)
                      .filter(chosenAllow)
                      .map((transition) => (
                        <button
                          key={transition}
                          type="button"
                          className={transition === 'retire' ? 'button button--danger' : 'button'}
                          onClick={() => {
                            confirmMany(transition);
                          }}
                        >
                          {t(`identityAccess.entities.transitions.${transition}.action`)}
                        </button>
                      ))}
                  </>
                ),
              }}
            >
              <thead>
                <tr>
                  <SelectAllHeader
                    ids={entities.data.items.map((entity) => entity.id)}
                    selection={selection}
                  />
                  <th scope="col">{t('identityAccess.entities.code')}</th>
                  <th scope="col">{t('identityAccess.entities.name')}</th>
                  <th scope="col">{t('identityAccess.entities.sponsor')}</th>
                  <th scope="col">{t('identityAccess.entities.status')}</th>
                  <th scope="col">
                    <span className="visually-hidden">{t('common.table.actions')}</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {entities.data.items.map((entity) => {
                  const name = labelOf(entity.name, language);
                  return (
                    <tr key={entity.id}>
                      <SelectRowCell id={entity.id} name={name} selection={selection} />
                      <td dir="ltr" className="cell--ltr">
                        {entity.code}
                      </td>
                      <td>{name}</td>
                      <td>
                        {entity.sponsorUserId === null
                          ? '—'
                          : (sponsorNames.get(entity.sponsorUserId) ?? '—')}
                      </td>
                      <td>
                        <StatusBadge
                          label={t(`identityAccess.entityStatus.${entity.status}`)}
                          tone={entity.status === 'ACTIVE' ? 'positive' : 'neutral'}
                        />
                      </td>
                      <td className="cell--actions">
                        {entity.status !== 'RETIRED' && (
                          <button
                            type="button"
                            className="button"
                            onClick={() => {
                              setEditing({ mode: 'edit', entityId: entity.id });
                            }}
                          >
                            {t('common.actions.edit')}{' '}
                            <span className="visually-hidden">{name}</span>
                          </button>
                        )}
                        {TRANSITIONS[entity.status].map((transition) => (
                          <button
                            key={transition}
                            type="button"
                            className={transition === 'retire' ? 'button button--danger' : 'button'}
                            onClick={() => {
                              confirm(entity, transition);
                            }}
                          >
                            {t(`identityAccess.entities.transitions.${transition}.action`)}{' '}
                            <span className="visually-hidden">{name}</span>
                          </button>
                        ))}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </TableContainer>
            <Pagination
              page={entities.data.page}
              pageSize={entities.data.pageSize}
              totalCount={entities.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}

      <Dialog
        open={editing !== null}
        title={
          editing?.mode === 'edit'
            ? t('identityAccess.entities.editTitle')
            : t('identityAccess.entities.createTitle')
        }
        onClose={() => {
          setEditing(null);
        }}
      >
        {editing !== null && (
          <ExternalEntityForm
            editing={editing}
            onClose={() => {
              setEditing(null);
            }}
            onSaved={() => {
              setEditing(null);
              entities.reload();
            }}
          />
        )}
      </Dialog>
      <ConfirmDialog
        confirmation={confirmation}
        onClose={() => {
          setConfirmation(null);
        }}
        onDone={() => {
          setConfirmation(null);
          entities.reload();
        }}
      />
    </>
  );
}

interface ExternalEntityFormProps {
  editing: Editing;
  onClose: () => void;
  onSaved: () => void;
}

function ExternalEntityForm({ editing, onClose, onSaved }: ExternalEntityFormProps): ReactElement {
  const { t } = useI18n();
  const entityId = editing.mode === 'edit' ? editing.entityId : null;
  const load = useCallback(
    async (signal: AbortSignal) => {
      if (entityId === null) {
        return null;
      }
      const entity = await externalEntitiesApi.get(entityId, signal);
      const sponsorId = entity.data.sponsorUserId;
      // A sponsor the caller can no longer read is shown as none; saving then keeps or replaces it explicitly.
      const sponsor =
        sponsorId === null
          ? null
          : await usersApi.get(sponsorId, signal).then(
              (response) => response.data,
              () => null,
            );
      return { entity, sponsor };
    },
    [entityId],
  );
  const existing = useApiResource(load);

  if (existing.loading) {
    return <LoadingState />;
  }
  if (existing.error !== null) {
    return (
      <ErrorState
        error={existing.error}
        message={problemMessage(existing.error, t)}
        onRetry={existing.reload}
      />
    );
  }
  return (
    <ExternalEntityFields
      current={existing.data?.entity.data ?? null}
      etag={existing.data?.entity.etag ?? null}
      initialSponsor={existing.data?.sponsor ?? null}
      onClose={onClose}
      onSaved={onSaved}
    />
  );
}

interface ExternalEntityFieldsProps {
  current: ExternalEntityDetail | null;
  etag: string | null;
  initialSponsor: PickedUser | null;
  onClose: () => void;
  onSaved: () => void;
}

function ExternalEntityFields({
  current,
  etag,
  initialSponsor,
  onClose,
  onSaved,
}: ExternalEntityFieldsProps): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const [code, setCode] = useState(current?.code ?? '');
  const [nameAr, setNameAr] = useState(current?.name.ar ?? '');
  const [nameEn, setNameEn] = useState(current?.name.en ?? '');
  const [entityTypeItemId, setEntityTypeItemId] = useState(current?.entityTypeItemId ?? '');
  const [sponsor, setSponsor] = useState<PickedUser | null>(initialSponsor);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      code:
        current === null
          ? checkText(code, { required: true, maxLength: CODE_LENGTH, pattern: CODE_PATTERN })
          : null,
      'name.ar': checkText(nameAr, { required: true, script: 'ar' }),
      'name.en': checkText(nameEn, { required: true, script: 'en' }),
      entityTypeItemId: checkText(entityTypeItemId, { required: true, pattern: UUID_PATTERN }),
    });
    if (!valid) {
      return;
    }
    const changes = {
      name: { ar: nameAr.trim(), en: nameEn.trim() },
      entityTypeItemId: entityTypeItemId.trim(),
      sponsorUserId: sponsor?.id ?? null,
    };
    const result = await save.run(() =>
      current === null
        ? externalEntitiesApi.create({ ...changes, code: code.trim() })
        : externalEntitiesApi.update(current.id, changes, etag),
    );
    if (result.ok) {
      onSaved();
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      {current === null ? (
        <TextField
          label={t('identityAccess.entities.code')}
          name="code"
          required
          dir="ltr"
          value={code}
          onChange={setCode}
          hint={t('identityAccess.codeHint')}
          error={save.fieldErrors.code}
        />
      ) : (
        <p className="form__readonly">
          <span className="field__label">{t('identityAccess.entities.code')}</span>
          <span dir="ltr">{current.code}</span>
        </p>
      )}
      <TextField
        label={t('identityAccess.entities.nameAr')}
        name="name.ar"
        required
        dir="rtl"
        value={nameAr}
        onChange={setNameAr}
        error={save.fieldErrors['name.ar']}
      />
      <TextField
        label={t('identityAccess.entities.nameEn')}
        name="name.en"
        required
        dir="ltr"
        value={nameEn}
        onChange={setNameEn}
        error={save.fieldErrors['name.en']}
      />
      <TextField
        label={t('identityAccess.entities.entityType')}
        name="entityTypeItemId"
        required
        dir="ltr"
        value={entityTypeItemId}
        onChange={setEntityTypeItemId}
        autoComplete="off"
        hint={t('identityAccess.entities.entityTypeHint')}
        error={save.fieldErrors.entityTypeItemId}
      />
      <UserPicker
        label={t('identityAccess.entities.sponsor')}
        userType="INTERNAL"
        value={sponsor}
        onChange={setSponsor}
        hint={t('identityAccess.entities.sponsorHint')}
        error={save.fieldErrors.sponsorUserId}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
