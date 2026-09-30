import { type SyntheticEvent, type ReactElement, useCallback, useRef, useState } from 'react';
import { useSearchParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

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

/** ADM-012 Departments: create, edit, activate and deactivate. A department is never deleted (RETAIN). */
export function DepartmentListPage(): ReactElement {
  const { t, language } = useI18n();
  const [params, setParams] = useSearchParams();
  const [editing, setEditing] = useState<Editing | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const { lookups } = useOrganizationLookups(language);

  const q = params.get('q') ?? undefined;
  const activeParam = params.get('isActive');
  const isActive = activeParam === 'true' ? true : activeParam === 'false' ? false : undefined;
  const page = Math.max(1, Number(params.get('page') ?? '1') || 1);

  const load = useCallback(
    (signal: AbortSignal) =>
      departmentsApi.list({ q, isActive, page, pageSize: PAGE_SIZE }, signal),
    [q, isActive, page],
  );
  const departments = useApiResource(load);

  const [draftQ, setDraftQ] = useState(q ?? '');
  const [draftActive, setDraftActive] = useState(activeParam ?? '');
  const applyFilters = (event: SyntheticEvent) => {
    event.preventDefault();
    const next = new URLSearchParams();
    if (draftQ.trim() !== '') {
      next.set('q', draftQ.trim());
    }
    if (draftActive !== '') {
      next.set('isActive', draftActive);
    }
    setParams(next);
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
          <button
            type="button"
            className="button button--primary"
            onClick={() => {
              setEditing({ mode: 'create' });
            }}
          >
            {t('identityAccess.departments.create')}
          </button>
        }
      />

      <form className="filters" onSubmit={applyFilters} aria-label={t('common.filters.label')}>
        <TextField
          label={t('identityAccess.departments.search')}
          name="q"
          type="search"
          value={draftQ}
          onChange={setDraftQ}
        />
        <SelectField
          label={t('identityAccess.departments.status')}
          name="isActive"
          value={draftActive}
          placeholder={t('common.filters.any')}
          options={[
            { value: 'true', label: t('identityAccess.departments.active') },
            { value: 'false', label: t('identityAccess.departments.inactive') },
          ]}
          onChange={setDraftActive}
        />
        <div className="filters__actions">
          <button type="submit" className="button button--primary">
            {t('common.filters.apply')}
          </button>
        </div>
      </form>

      {departments.loading && <LoadingState />}
      {departments.error !== null && (
        <ErrorState message={problemMessage(departments.error, t)} onRetry={departments.reload} />
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
            <TableContainer caption={t('identityAccess.departments.title')}>
              <thead>
                <tr>
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
                        {department.isActive
                          ? t('identityAccess.departments.deactivate')
                          : t('identityAccess.departments.activate')}{' '}
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
              onPageChange={(next) => {
                const updated = new URLSearchParams(params);
                updated.set('page', String(next));
                setParams(updated);
              }}
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
    return <ErrorState message={problemMessage(existing.error, t)} onRetry={existing.reload} />;
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
