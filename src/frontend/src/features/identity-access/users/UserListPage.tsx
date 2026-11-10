import { type ReactElement, useCallback, useState } from 'react';
import { Link } from 'react-router';

import { ImportAction } from '@/features/data-import/ImportAction.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';
import { useSearchText } from '@/shared/ui/useSearchText.ts';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { usersApi } from '../api/identityAccessApi.ts';
import { ConfirmDialog, type Confirmation } from '../components/ConfirmDialog.tsx';
import { useCan } from '../permissions/permissions.ts';
import {
  type UserQuery,
  type UserSort,
  type UserStatus,
  type UserSummary,
  type UserType,
} from '../api/types.ts';
import { labelOf, type OrganizationLookups, useOrganizationLookups } from '../lookups.ts';
import { problemMessage } from '../problems.ts';

const PAGE_SIZE = 25;
const USER_STATUSES: UserStatus[] = ['ACTIVE', 'DISABLED'];
const USER_TYPES: UserType[] = ['INTERNAL', 'EXTERNAL', 'SERVICE'];

/** Unless the type filter asks for them, the list shows people only: the platform's service accounts run its jobs and are not administered. */
const PEOPLE = 'INTERNAL,EXTERNAL' as const;
const USER_SORTS: UserSort[] = [
  'displayName:asc',
  'displayName:desc',
  'username:asc',
  'username:desc',
];
const SORT_LABELS: Record<UserSort, TranslationKey> = {
  'displayName:asc': 'identityAccess.users.list.sort.displayNameAsc',
  'displayName:desc': 'identityAccess.users.list.sort.displayNameDesc',
  'username:asc': 'identityAccess.users.list.sort.usernameAsc',
  'username:desc': 'identityAccess.users.list.sort.usernameDesc',
};

function oneOf<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((candidate) => candidate === value);
}

type UserListQuery = UserQuery & { sort: UserSort; page: number };

function queryFrom(params: URLSearchParams): UserListQuery {
  const page = Number(params.get('page') ?? '1');
  return {
    q: params.get('q') ?? undefined,
    status: oneOf(params.get('status'), USER_STATUSES),
    userType: oneOf(params.get('userType'), USER_TYPES),
    departmentId: params.get('departmentId') ?? undefined,
    externalEntityId: params.get('externalEntityId') ?? undefined,
    sort: oneOf(params.get('sort'), USER_SORTS) ?? 'displayName:asc',
    page: Number.isInteger(page) && page > 0 ? page : 1,
  };
}

interface UserFiltersProps {
  query: UserListQuery;
  filtered: boolean;
  lookups: OrganizationLookups | undefined;
}

/** The drop-downs apply as they are chosen and the search as it is typed, all in the URL, so Clear filters or Back resets them too. */
function UserFilters({ query, filtered, lookups }: UserFiltersProps): ReactElement {
  const { t, language } = useI18n();
  const { setFilter, setFilters } = useListParams();
  const [text, setText] = useSearchText(query.q ?? '', (value) => {
    setFilter('q', value, { replace: true });
  });

  const chip = (name: string, label: TranslationKey, value: string) => ({
    name,
    text: t('common.filters.inline', { label: t(label), value }),
    onRemove: () => {
      setFilter(name, '');
    },
  });
  const nameOf = (
    list: { id: string; name: Parameters<typeof labelOf>[0] }[] | undefined,
    id: string,
  ) => {
    const found = list?.find((candidate) => candidate.id === id);
    return found === undefined ? id : labelOf(found.name, language);
  };
  const { q, status, userType, departmentId, externalEntityId } = query;

  return (
    <FilterBar
      onClearAll={() => {
        setFilters({ q: '', status: '', userType: '', departmentId: '', externalEntityId: '' });
      }}
      chips={[
        ...(q === undefined ? [] : [chip('q', 'identityAccess.users.list.search', q)]),
        ...(status === undefined
          ? []
          : [
              chip(
                'status',
                'identityAccess.users.fields.status',
                t(`identityAccess.userStatus.${status}`),
              ),
            ]),
        ...(userType === undefined
          ? []
          : [
              chip(
                'userType',
                'identityAccess.users.fields.userType',
                t(`identityAccess.userType.${userType}`),
              ),
            ]),
        ...(departmentId === undefined
          ? []
          : [
              chip(
                'departmentId',
                'identityAccess.users.fields.department',
                nameOf(lookups?.departments, departmentId),
              ),
            ]),
        ...(externalEntityId === undefined
          ? []
          : [
              chip(
                'externalEntityId',
                'identityAccess.users.fields.externalEntity',
                nameOf(lookups?.entities, externalEntityId),
              ),
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
          label={t('identityAccess.users.list.search')}
          name="q"
          type="search"
          value={text}
          onChange={setText}
        />
        <SelectField
          label={t('identityAccess.users.fields.status')}
          name="status"
          value={query.status ?? ''}
          placeholder={t('common.filters.any')}
          options={USER_STATUSES.map((value) => ({
            value,
            label: t(`identityAccess.userStatus.${value}`),
          }))}
          onChange={(value) => {
            setFilter('status', value);
          }}
        />
        <SelectField
          label={t('identityAccess.users.fields.userType')}
          name="userType"
          value={query.userType ?? ''}
          placeholder={t('common.filters.any')}
          options={USER_TYPES.map((value) => ({
            value,
            label: t(`identityAccess.userType.${value}`),
          }))}
          onChange={(value) => {
            setFilter('userType', value);
          }}
        />
        <SelectField
          label={t('identityAccess.users.fields.department')}
          name="departmentId"
          value={query.departmentId ?? ''}
          placeholder={t('common.filters.any')}
          options={(lookups?.departments ?? []).map((d) => ({
            value: d.id,
            label: labelOf(d.name, language),
          }))}
          onChange={(value) => {
            setFilter('departmentId', value);
          }}
        />
        <SelectField
          label={t('identityAccess.users.fields.externalEntity')}
          name="externalEntityId"
          value={query.externalEntityId ?? ''}
          placeholder={t('common.filters.any')}
          options={(lookups?.entities ?? []).map((e) => ({
            value: e.id,
            label: labelOf(e.name, language),
          }))}
          onChange={(value) => {
            setFilter('externalEntityId', value);
          }}
        />
        <SelectField
          label={t('common.filters.sort')}
          name="sort"
          value={query.sort}
          options={USER_SORTS.map((value) => ({
            value,
            label: t(SORT_LABELS[value]),
          }))}
          onChange={(value) => {
            setFilter('sort', value === 'displayName:asc' ? '' : value);
          }}
        />
        {filtered && (
          <div className="filters__actions">
            <Link className="button" to="/admin/users">
              {t('common.filters.clear')}
            </Link>
          </div>
        )}
      </form>
    </FilterBar>
  );
}

/** ADM-002 User List. Filters live in the URL, so a filtered list can be bookmarked and Back restores it. */
export function UserListPage(): ReactElement {
  const selection = useRowSelection();
  const { t, language } = useI18n();
  const mayManage = useCan('USER_MANAGE');
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const { params, goToPage } = useListParams();
  const query = queryFrom(params);
  const { lookups } = useOrganizationLookups(language);

  const { q, status, userType, departmentId, externalEntityId, sort, page } = query;
  const load = useCallback(
    (signal: AbortSignal) =>
      usersApi.list(
        {
          q,
          status,
          userType: userType ?? PEOPLE,
          departmentId,
          externalEntityId,
          sort,
          page,
          pageSize: PAGE_SIZE,
        },
        signal,
      ),
    [q, status, userType, departmentId, externalEntityId, sort, page],
  );
  const users = useApiResource(load, { keepPreviousResult: true });

  const filtered = [q, status, userType, departmentId, externalEntityId].some(
    (v) => v !== undefined,
  );

  return (
    <>
      <PageHeader
        title={t('identityAccess.users.list.title')}
        description={t('identityAccess.users.list.description')}
        actions={
          <>
            <ImportAction
              collectionPath="/users"
              registerName={t('identityAccess.users.list.title')}
              onImported={users.reload}
            />
            <Link className="button button--primary" to="/admin/users/new">
              {t('identityAccess.users.list.create')}
            </Link>
          </>
        }
      />

      <UserFilters query={query} filtered={filtered} lookups={lookups} />

      {users.loading && users.data === undefined && <LoadingState />}
      {users.error !== null && (
        <ErrorState
          error={users.error}
          message={problemMessage(users.error, t)}
          onRetry={users.reload}
        />
      )}
      {users.data !== undefined &&
        (users.data.items.length === 0 ? (
          <EmptyState
            title={
              filtered
                ? t('identityAccess.users.list.emptyFiltered')
                : t('identityAccess.users.list.empty')
            }
          />
        ) : (
          <>
            <TableContainer
              caption={t('identityAccess.users.list.caption')}
              selection={{
                selection,
                exportName: 'users',
                actions: (
                  <>
                    {mayManage && (
                      <UserBulkActions
                        users={users.data.items.filter(
                          (user) => selection.selected.has(user.id) && user.userType !== 'SERVICE',
                        )}
                        onConfirm={setConfirmation}
                        onDone={selection.clear}
                      />
                    )}
                  </>
                ),
              }}
            >
              <thead>
                <tr>
                  <SelectAllHeader
                    ids={users.data.items.map((user) => user.id)}
                    selection={selection}
                  />
                  <th scope="col">{t('identityAccess.users.fields.displayName')}</th>
                  <th scope="col">{t('identityAccess.users.fields.username')}</th>
                  <th scope="col">{t('identityAccess.users.fields.email')}</th>
                  <th scope="col">{t('identityAccess.users.fields.userType')}</th>
                  <th scope="col">{t('identityAccess.users.fields.accountSource')}</th>
                  <th scope="col">{t('identityAccess.users.fields.organization')}</th>
                  <th scope="col">{t('identityAccess.users.fields.status')}</th>
                  {mayManage && <th scope="col">{t('common.table.actions')}</th>}
                </tr>
              </thead>
              <tbody>
                {users.data.items.map((user) => (
                  <tr key={user.id}>
                    <SelectRowCell id={user.id} name={user.displayName} selection={selection} />
                    <td>
                      <Link to={`/admin/users/${user.id}`}>{user.displayName}</Link>
                    </td>
                    <td dir="ltr" className="cell--ltr">
                      {user.username}
                    </td>
                    <td dir="ltr" className="cell--ltr cell--break">
                      {user.email}
                    </td>
                    <td>{t(`identityAccess.userType.${user.userType}`)}</td>
                    <td>
                      {user.accountSource === undefined
                        ? '—'
                        : t(`identityAccess.accountSource.${user.accountSource}`)}
                    </td>
                    <td>
                      {lookups?.departmentName(user.departmentId) ??
                        lookups?.entityName(user.externalEntityId) ??
                        '—'}
                    </td>
                    <td>
                      <StatusBadge
                        label={t(`identityAccess.userStatus.${user.status}`)}
                        tone={user.status === 'ACTIVE' ? 'positive' : 'negative'}
                      />
                    </td>
                    {mayManage && (
                      <td className="cell--actions">
                        {user.userType === 'SERVICE' ? (
                          <span className="cell__aside">
                            {t('identityAccess.users.list.serviceAccount')}
                          </span>
                        ) : (
                          <div className="row-actions">
                            <Link className="button" to={`/admin/users/${user.id}/edit`}>
                              {t('common.actions.edit')}{' '}
                              <span className="visually-hidden">{user.displayName}</span>
                            </Link>
                            <button
                              type="button"
                              className="button button--danger"
                              onClick={() => {
                                setConfirmation({
                                  title: t('identityAccess.users.delete.title'),
                                  body: t('identityAccess.users.delete.body', {
                                    name: user.displayName,
                                  }),
                                  confirmLabel: t('identityAccess.users.delete.confirm'),
                                  destructive: true,
                                  action: () => usersApi.remove(user.id, null),
                                });
                              }}
                            >
                              {t('identityAccess.users.delete.short')}{' '}
                              <span className="visually-hidden">{user.displayName}</span>
                            </button>
                          </div>
                        )}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </TableContainer>
            <Pagination
              page={users.data.page}
              pageSize={users.data.pageSize}
              totalCount={users.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}
      <ConfirmDialog
        confirmation={confirmation}
        onClose={() => {
          setConfirmation(null);
        }}
        onDone={() => {
          setConfirmation(null);
          users.reload();
        }}
      />
    </>
  );
}

/** Disable or delete the chosen users, confirmed by name. The API refuses one's own account and says why. */
function UserBulkActions({
  users,
  onConfirm,
  onDone,
}: {
  users: UserSummary[];
  onConfirm: (confirmation: Confirmation) => void;
  onDone: () => void;
}): ReactElement | null {
  const { t } = useI18n();
  const active = users.filter((user) => user.status === 'ACTIVE');
  const names = (chosen: UserSummary[]) => chosen.map((user) => user.displayName).join('، ');
  return (
    <>
      {active.length > 0 && (
        <button
          type="button"
          className="button"
          onClick={() => {
            onConfirm({
              title: t('identityAccess.users.bulk.disableTitle', { count: active.length }),
              body: t('identityAccess.users.bulk.disableBody', { names: names(active) }),
              confirmLabel: t('identityAccess.users.bulk.disable', { count: active.length }),
              destructive: true,
              action: async () => {
                for (const user of active) {
                  await usersApi.disable(user.id, null);
                }
                onDone();
              },
            });
          }}
        >
          {t('identityAccess.users.bulk.disable', { count: active.length })}
        </button>
      )}
      {users.length > 0 && (
        <button
          type="button"
          className="button button--danger"
          onClick={() => {
            onConfirm({
              title: t('identityAccess.users.bulk.deleteTitle', { count: users.length }),
              body: t('identityAccess.users.bulk.deleteBody', { names: names(users) }),
              confirmLabel: t('identityAccess.users.bulk.delete', { count: users.length }),
              destructive: true,
              action: async () => {
                for (const user of users) {
                  await usersApi.remove(user.id, null);
                }
                onDone();
              },
            });
          }}
        >
          {t('identityAccess.users.bulk.delete', { count: users.length })}
        </button>
      )}
    </>
  );
}
