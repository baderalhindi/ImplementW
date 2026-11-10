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
import { useSearchText } from '@/shared/ui/useSearchText.ts';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { ToggleLabel } from '@/shared/ui/ToggleLabel.tsx';

import { departmentsApi } from '../api/identityAccessApi.ts';
import { type DepartmentSummary } from '../api/types.ts';
import { type Confirmation, ConfirmDialog } from '../components/ConfirmDialog.tsx';
import {
  checkText,
  CODE_LENGTH,
  CODE_PATTERN,
  optionalText,
  useFocusFirstError,
  useSaveAction,
} from '../forms.ts';
import { labelOf, useOrganizationLookups } from '../lookups.ts';
import { problemMessage } from '../problems.ts';

const PAGE_SIZE = 25;

type Editing = { mode: 'create' } | { mode: 'edit'; departmentId: string };

/** ADM-012 Departments: create, edit, activate, deactivate and delete (client review, item 6). A deleted department's row stays (RETAIN). */
export function DepartmentListPage(): ReactElement {
  const { t, language } = useI18n();
  const { params, page, setFilter, setFilters, goToPage } = useListParams();
  const [editing, setEditing] = useState<Editing | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const { lookups } = useOrganizationLookups(language);
  const selection = useRowSelection();

  const q = params.get('q') ?? undefined;
  const activeParam = params.get('isActive');
  const isActive = activeParam === 'true' ? true : activeParam === 'false' ? false : undefined;

  const load = useCallback(
    (signal: AbortSignal) =>
      departmentsApi.list({ q, isActive, page, pageSize: PAGE_SIZE }, signal),
    [q, isActive, page],
  );
  const departments = useApiResource(load, { keepPreviousResult: true });

  const [searchText, setSearchText] = useSearchText(q ?? '', (text) => {
    setFilter('q', text, { replace: true });
  });

  const confirmDelete = (chosen: DepartmentSummary[]) => {
    setConfirmation({
      title: t('identityAccess.departments.deleteTitle', { count: chosen.length }),
      body: t('identityAccess.departments.deleteBody', {
        names: chosen.map((department) => labelOf(department.name, language)).join('، '),
      }),
      confirmLabel: t('identityAccess.departments.delete'),
      destructive: true,
      action: async () => {
        for (const department of chosen) {
          await departmentsApi.remove(department.id);
        }
        selection.clear();
      },
    });
  };

  const toggle = (department: DepartmentSummary) => {
    const name = labelOf(department.name, language);
    setConfirmation(
      department.isActive
        ? {
            title: t('identityAccess.departments.deactivateTitle'),
            body: t('identityAccess.departments.deactivateBody', { name }),
            confirmLabel: t('identityAccess.departments.deactivate'),
            destructive: true,
            action: () => departmentsApi.deactivate(department.id),
          }
        : {
            title: t('identityAccess.departments.activateTitle'),
            body: t('identityAccess.departments.activateBody', { name }),
            confirmLabel: t('identityAccess.departments.activate'),
            destructive: false,
            action: () => departmentsApi.activate(department.id),
          },
    );
  };

  return (
    <>
      <PageHeader
        title={t('identityAccess.departments.title')}
        description={t('identityAccess.departments.description')}
        actions={
          <>
            <ImportAction
              collectionPath="/departments"
              registerName={t('identityAccess.departments.title')}
              onImported={departments.reload}
            />
            <button
              type="button"
              className="button button--primary"
              onClick={() => {
                setEditing({ mode: 'create' });
              }}
            >
              {t('identityAccess.departments.create')}
            </button>
          </>
        }
      />

      <FilterBar
        onClearAll={() => {
          setFilters({ q: '', isActive: '' });
        }}
        chips={[
          ...(q === undefined
            ? []
            : [
                {
                  name: 'q',
                  text: t('common.filters.inline', {
                    label: t('identityAccess.departments.search'),
                    value: q,
                  }),
                  onRemove: () => {
                    setFilter('q', '');
                  },
                },
              ]),
          ...(isActive === undefined
            ? []
            : [
                {
                  name: 'isActive',
                  text: t('common.filters.inline', {
                    label: t('identityAccess.departments.status'),
                    value: t(
                      isActive
                        ? 'identityAccess.departments.active'
                        : 'identityAccess.departments.inactive',
                    ),
                  }),
                  onRemove: () => {
                    setFilter('isActive', '');
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
            label={t('identityAccess.departments.search')}
            name="q"
            type="search"
            value={searchText}
            onChange={setSearchText}
          />
          <SelectField
            label={t('identityAccess.departments.status')}
            name="isActive"
            value={activeParam ?? ''}
            placeholder={t('common.filters.any')}
            options={[
              { value: 'true', label: t('identityAccess.departments.active') },
              { value: 'false', label: t('identityAccess.departments.inactive') },
            ]}
            onChange={(value) => {
              setFilter('isActive', value);
            }}
          />
        </form>
      </FilterBar>

      {departments.loading && departments.data === undefined && <LoadingState />}
      {departments.error !== null && (
        <ErrorState
          error={departments.error}
          message={problemMessage(departments.error, t)}
          onRetry={departments.reload}
        />
      )}
      {departments.data !== undefined &&
        (departments.data.items.length === 0 ? (
          <EmptyState
            title={
              q === undefined && isActive === undefined
                ? t('identityAccess.departments.empty')
                : t('identityAccess.departments.emptyFiltered')
            }
          />
        ) : (
          <>
            <TableContainer
              caption={t('identityAccess.departments.title')}
              selection={{
                selection,
                exportName: 'departments',
                actions: (
                  <>
                    <button
                      type="button"
                      className="button"
                      onClick={() => {
                        const chosen =
                          departments.data?.items.filter(
                            (department) =>
                              department.isActive && selection.selected.has(department.id),
                          ) ?? [];
                        setConfirmation({
                          title: t('identityAccess.departments.deactivateTitle'),
                          body: chosen
                            .map((department) => labelOf(department.name, language))
                            .join('، '),
                          confirmLabel: t('identityAccess.departments.deactivate'),
                          destructive: true,
                          action: async () => {
                            for (const department of chosen) {
                              await departmentsApi.deactivate(department.id);
                            }
                            selection.clear();
                          },
                        });
                      }}
                    >
                      {t('identityAccess.departments.deactivate')}
                    </button>
                    <button
                      type="button"
                      className="button button--danger"
                      onClick={() => {
                        confirmDelete(
                          departments.data?.items.filter((department) =>
                            selection.selected.has(department.id),
                          ) ?? [],
                        );
                      }}
                    >
                      {t('identityAccess.departments.deleteSelected', {
                        count: selection.selected.size,
                      })}
                    </button>
                  </>
                ),
              }}
            >
              <thead>
                <tr>
                  <SelectAllHeader
                    ids={departments.data.items.map((department) => department.id)}
                    selection={selection}
                  />
                  <th scope="col">{t('identityAccess.departments.code')}</th>
                  <th scope="col">{t('identityAccess.departments.name')}</th>
                  <th scope="col">{t('identityAccess.departments.parent')}</th>
                  <th scope="col">{t('identityAccess.departments.status')}</th>
                  <th scope="col">
                    <span className="visually-hidden">{t('common.table.actions')}</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {departments.data.items.map((department) => (
                  <tr key={department.id}>
                    <SelectRowCell
                      id={department.id}
                      name={labelOf(department.name, language)}
                      selection={selection}
                    />
                    <td dir="ltr" className="cell--ltr">
                      {department.code}
                    </td>
                    <td>{labelOf(department.name, language)}</td>
                    <td>{lookups?.departmentName(department.parentDepartmentId) ?? '—'}</td>
                    <td>
                      <StatusBadge
                        label={
                          department.isActive
                            ? t('identityAccess.departments.active')
                            : t('identityAccess.departments.inactive')
                        }
                        tone={department.isActive ? 'positive' : 'neutral'}
                      />
                    </td>
                    <td className="cell--actions">
                      <button
                        type="button"
                        className="button"
                        onClick={() => {
                          setEditing({ mode: 'edit', departmentId: department.id });
                        }}
                      >
                        {t('common.actions.edit')}{' '}
                        <span className="visually-hidden">
                          {labelOf(department.name, language)}
                        </span>
                      </button>
                      <button
                        type="button"
                        className="button"
                        onClick={() => {
                          toggle(department);
                        }}
                      >
                        <ToggleLabel
                          current={t(
                            department.isActive
                              ? 'identityAccess.departments.deactivate'
                              : 'identityAccess.departments.activate',
                          )}
                          other={t(
                            department.isActive
                              ? 'identityAccess.departments.activate'
                              : 'identityAccess.departments.deactivate',
                          )}
                        />{' '}
                        <span className="visually-hidden">
                          {labelOf(department.name, language)}
                        </span>
                      </button>
                      <button
                        type="button"
                        className="button button--danger"
                        onClick={() => {
                          confirmDelete([department]);
                        }}
                      >
                        {t('identityAccess.departments.delete')}{' '}
                        <span className="visually-hidden">
                          {labelOf(department.name, language)}
                        </span>
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </TableContainer>
            <Pagination
              page={departments.data.page}
              pageSize={departments.data.pageSize}
              totalCount={departments.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}

      <Dialog
        open={editing !== null}
        title={
          editing?.mode === 'edit'
            ? t('identityAccess.departments.editTitle')
            : t('identityAccess.departments.createTitle')
        }
        onClose={() => {
          setEditing(null);
        }}
      >
        {editing !== null && (
          <DepartmentForm
            editing={editing}
            departments={lookups?.departments ?? []}
            onClose={() => {
              setEditing(null);
            }}
            onSaved={() => {
              setEditing(null);
              departments.reload();
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
          departments.reload();
        }}
      />
    </>
  );
}

interface DepartmentFormProps {
  editing: Editing;
  departments: DepartmentSummary[];
  onClose: () => void;
  onSaved: () => void;
}

function DepartmentForm({
  editing,
  departments,
  onClose,
  onSaved,
}: DepartmentFormProps): ReactElement {
  const { t } = useI18n();
  const departmentId = editing.mode === 'edit' ? editing.departmentId : null;
  const load = useCallback(
    (signal: AbortSignal) =>
      departmentId === null ? Promise.resolve(null) : departmentsApi.get(departmentId, signal),
    [departmentId],
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
    <DepartmentFields
      existing={existing.data ?? null}
      departments={departments}
      onClose={onClose}
      onSaved={onSaved}
    />
  );
}

function DepartmentFields({
  existing,
  departments,
  onClose,
  onSaved,
}: {
  existing: Awaited<ReturnType<typeof departmentsApi.get>> | null;
  departments: DepartmentSummary[];
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction();
  useFocusFirstError(formRef, save.fieldErrors);
  const current = existing?.data;
  const [code, setCode] = useState(current?.code ?? '');
  const [nameAr, setNameAr] = useState(current?.name.ar ?? '');
  const [nameEn, setNameEn] = useState(current?.name.en ?? '');
  const [parentId, setParentId] = useState(current?.parentDepartmentId ?? '');
  const [directoryReference, setDirectoryReference] = useState(current?.directoryReference ?? '');

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const valid = save.validate({
      code:
        current === undefined
          ? checkText(code, { required: true, maxLength: CODE_LENGTH, pattern: CODE_PATTERN })
          : null,
      'name.ar': checkText(nameAr, { required: true }),
      'name.en': checkText(nameEn, { required: true }),
      directoryReference: checkText(directoryReference, {}),
    });
    if (!valid) {
      return;
    }
    const changes = {
      name: { ar: nameAr.trim(), en: nameEn.trim() },
      parentDepartmentId: parentId === '' ? null : parentId,
      directoryReference: optionalText(directoryReference),
    };
    const result = await save.run(() =>
      current === undefined
        ? departmentsApi.create({ ...changes, code: code.trim() })
        : departmentsApi.update(current.id, changes, existing?.etag ?? null),
    );
    if (result.ok) {
      onSaved();
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      {current === undefined ? (
        <TextField
          label={t('identityAccess.departments.code')}
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
          <span className="field__label">{t('identityAccess.departments.code')}</span>
          <span dir="ltr">{current.code}</span>
        </p>
      )}
      <TextField
        label={t('identityAccess.departments.nameAr')}
        name="name.ar"
        required
        dir="rtl"
        value={nameAr}
        onChange={setNameAr}
        error={save.fieldErrors['name.ar']}
      />
      <TextField
        label={t('identityAccess.departments.nameEn')}
        name="name.en"
        required
        dir="ltr"
        value={nameEn}
        onChange={setNameEn}
        error={save.fieldErrors['name.en']}
      />
      <SelectField
        label={t('identityAccess.departments.parent')}
        name="parentDepartmentId"
        value={parentId}
        placeholder={t('identityAccess.departments.noParent')}
        options={departments
          .filter((department) => department.id !== current?.id)
          .map((department) => ({
            value: department.id,
            label: labelOf(department.name, language),
          }))}
        onChange={setParentId}
        error={save.fieldErrors.parentDepartmentId}
      />
      <TextField
        label={t('identityAccess.departments.directoryReference')}
        name="directoryReference"
        dir="ltr"
        value={directoryReference}
        onChange={setDirectoryReference}
        hint={t('identityAccess.departments.directoryReferenceHint')}
        error={save.fieldErrors.directoryReference}
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
