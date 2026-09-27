import { type SyntheticEvent, type ReactElement, useCallback, useState } from 'react';
import { Link, useSearchParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { usersApi } from '../api/identityAccessApi.ts';
import { type UserQuery, type UserSort, type UserStatus, type UserType } from '../api/types.ts';
import { StatusBadge } from '../components/StatusBadge.tsx';
import { labelOf, type OrganizationLookups, useOrganizationLookups } from '../lookups.ts';
import { problemMessage } from '../problems.ts';

const PAGE_SIZE = 25;
const USER_STATUSES: UserStatus[] = ['ACTIVE', 'DISABLED'];
const USER_TYPES: UserType[] = ['INTERNAL', 'EXTERNAL', 'SERVICE'];
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

/** Keyed on the URL, so Clear filters or Back resets the inputs to what the list is showing. */
function UserFilters({ query, filtered, lookups }: UserFiltersProps): ReactElement {
  const { t, language } = useI18n();
  const [, setParams] = useSearchParams();
  const [draft, setDraft] = useState({
    q: query.q ?? '',
    status: query.status ?? '',
    userType: query.userType ?? '',
    departmentId: query.departmentId ?? '',
    externalEntityId: query.externalEntityId ?? '',
    sort: query.sort,
  });

  const applyFilters = (event: SyntheticEvent) => {
    event.preventDefault();
    const next = new URLSearchParams();
    for (const [name, value] of Object.entries(draft)) {
      if (value.trim() !== '' && !(name === 'sort' && value === 'displayName:asc')) {
        next.set(name, value.trim());
      }
    }
    setParams(next);
  };

  return (
    <form className="filters" onSubmit={applyFilters} aria-label={t('common.filters.label')}>
      <TextField
        label={t('identityAccess.users.list.search')}
        name="q"
        type="search"
        value={draft.q}
        onChange={(value) => {
          setDraft({ ...draft, q: value });
        }}
      />
      <SelectField
        label={t('identityAccess.users.fields.status')}
        name="status"
        value={draft.status}
        placeholder={t('common.filters.any')}
        options={USER_STATUSES.map((value) => ({
          value,
          label: t(`identityAccess.userStatus.${value}`),
        }))}
        onChange={(value) => {
          setDraft({ ...draft, status: oneOf(value, USER_STATUSES) ?? '' });
        }}
      />
      <SelectField
        label={t('identityAccess.users.fields.userType')}
        name="userType"
        value={draft.userType}
        placeholder={t('common.filters.any')}
        options={USER_TYPES.map((value) => ({
          value,
          label: t(`identityAccess.userType.${value}`),
        }))}
        onChange={(value) => {
          setDraft({ ...draft, userType: oneOf(value, USER_TYPES) ?? '' });
        }}
      />
      <SelectField
        label={t('identityAccess.users.fields.department')}
        name="departmentId"
        value={draft.departmentId}
        placeholder={t('common.filters.any')}
        options={(lookups?.departments ?? []).map((d) => ({
          value: d.id,
          label: labelOf(d.name, language),
        }))}
        onChange={(value) => {
          setDraft({ ...draft, departmentId: value });
        }}
      />
      <SelectField
        label={t('identityAccess.users.fields.externalEntity')}
        name="externalEntityId"
        value={draft.externalEntityId}
        placeholder={t('common.filters.any')}
        options={(lookups?.entities ?? []).map((e) => ({
          value: e.id,
          label: labelOf(e.name, language),
        }))}
        onChange={(value) => {
          setDraft({ ...draft, externalEntityId: value });
        }}
      />
      <SelectField
        label={t('common.filters.sort')}
        name="sort"
        value={draft.sort}
        options={USER_SORTS.map((value) => ({
          value,
          label: t(SORT_LABELS[value]),
        }))}
        onChange={(value) => {
          setDraft({ ...draft, sort: oneOf(value, USER_SORTS) ?? 'displayName:asc' });
        }}
      />
      <div className="filters__actions">
        <button type="submit" className="button button--primary">
          {t('common.filters.apply')}
        </button>
        {filtered && (
          <Link className="button" to="/admin/users">
            {t('common.filters.clear')}
          </Link>
        )}
      </div>
    </form>
  );
}

/** ADM-002 User List. Filters live in the URL, so a filtered list can be bookmarked and Back restores it. */
export function UserListPage(): ReactElement {
  const { t, language } = useI18n();
  const [params, setParams] = useSearchParams();
  const query = queryFrom(params);
  const { lookups } = useOrganizationLookups(language);

  const { q, status, userType, departmentId, externalEntityId, sort, page } = query;
  const load = useCallback(
    (signal: AbortSignal) =>
      usersApi.list(
        { q, status, userType, departmentId, externalEntityId, sort, page, pageSize: PAGE_SIZE },
        signal,
      ),
    [q, status, userType, departmentId, externalEntityId, sort, page],
  );
  const users = useApiResource(load);

  const goToPage = (next: number) => {
    const updated = new URLSearchParams(params);
    updated.set('page', String(next));
    setParams(updated);
  };

  const filtered = [q, status, userType, departmentId, externalEntityId].some(
    (v) => v !== undefined,
  );

  return (
    <>
      <PageHeader
        title={t('identityAccess.users.list.title')}
        description={t('identityAccess.users.list.description')}
        actions={
          <Link className="button button--primary" to="/admin/users/new">
            {t('identityAccess.users.list.create')}
          </Link>
        }
      />

      <UserFilters key={params.toString()} query={query} filtered={filtered} lookups={lookups} />

      {users.loading && <LoadingState />}
      {users.error !== null && (
        <ErrorState message={problemMessage(users.error, t)} onRetry={users.reload} />
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
            <TableContainer caption={t('identityAccess.users.list.caption')}>
              <thead>
                <tr>
                  <th scope="col">{t('identityAccess.users.fields.displayName')}</th>
                  <th scope="col">{t('identityAccess.users.fields.username')}</th>
                  <th scope="col">{t('identityAccess.users.fields.email')}</th>
                  <th scope="col">{t('identityAccess.users.fields.userType')}</th>
                  <th scope="col">{t('identityAccess.users.fields.organization')}</th>
                  <th scope="col">{t('identityAccess.users.fields.status')}</th>
                </tr>
              </thead>
              <tbody>
                {users.data.items.map((user) => (
                  <tr key={user.id}>
                    <td>
                      <Link to={`/admin/users/${user.id}`}>{user.displayName}</Link>
                    </td>
                    <td dir="ltr" className="cell--ltr">
                      {user.username}
                    </td>
                    <td dir="ltr" className="cell--ltr">
                      {user.email}
                    </td>
                    <td>{t(`identityAccess.userType.${user.userType}`)}</td>
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
    </>
  );
}
