import { type ReactElement, type SyntheticEvent, useCallback, useState } from 'react';

import { ApiError, apiRequest } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { AmountField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { useCan } from '../permissions/permissions.ts';
import { problemMessage } from '../problems.ts';

// Review round 5, item 7: the project stages and their weights. A change is a new PROJECT_STAGE version with its effective date, on the engine
// the risk matrix and KPI targets use, so what was calculated under an earlier version keeps its weights.

const VERSIONS = '/configuration-versions';
const WEIGHT_TOTAL = 100;

interface StageRow {
  code: string;
  name: { ar: string; en: string };
  weightPercent: string;
  isActive: boolean;
}

interface VersionSummary {
  id: string;
  versionNo: number;
  lifecycleState: string;
  effectiveFrom: string | null;
}

interface VersionDetail extends VersionSummary {
  content: {
    projectStages: {
      code: string;
      name: { ar: string; en: string };
      sortOrder: number;
      weightPercent: number;
      isActive: boolean;
    }[];
  };
}

interface Loaded {
  familyId: string;
  versions: VersionSummary[];
  /** The stages to start from: the newest version that is not retired. */
  latest: VersionDetail | null;
}

async function readStages(signal: AbortSignal): Promise<Loaded> {
  const families = (
    await apiRequest<{ id: string; code: string }[] | { items: { id: string; code: string }[] }>(
      '/configuration-families',
      { signal },
    )
  ).data;
  const list = Array.isArray(families) ? families : families.items;
  const familyId = list.find((family) => family.code === 'PROJECT_STAGE')?.id ?? '';
  const versions = (
    await apiRequest<{ items: VersionSummary[] }>(VERSIONS, {
      query: { familyId, pageSize: 100 },
      signal,
    })
  ).data.items;
  const newest = versions.find((version) => version.lifecycleState !== 'RETIRED');
  const latest =
    newest === undefined
      ? null
      : (await apiRequest<VersionDetail>(`${VERSIONS}/${newest.id}`, { signal })).data;
  return { familyId, versions, latest };
}

function rowsOf(version: VersionDetail | null): StageRow[] {
  return [...(version?.content.projectStages ?? [])]
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((stage) => ({
      code: stage.code,
      name: stage.name,
      weightPercent: String(stage.weightPercent),
      isActive: stage.isActive,
    }));
}

/** A code for a new stage: stable once given, whatever the stage is later renamed. */
function nextCode(rows: readonly StageRow[]): string {
  let number = rows.length + 1;
  while (rows.some((row) => row.code === `STAGE_${String(number)}`)) {
    number += 1;
  }
  return `STAGE_${String(number)}`;
}

function totalOf(rows: readonly StageRow[]): number {
  return rows
    .filter((row) => row.isActive)
    .reduce((total, row) => total + Math.round(Number(row.weightPercent || 0) * 100), 0);
}

export function ProjectStagesPage(): ReactElement {
  const { t } = useI18n();
  const stages = useApiResource(readStages);
  const [notice, setNotice] = useState<string | null>(null);
  return (
    <>
      <PageHeader
        title={t('identityAccess.projectStages.title')}
        description={t('identityAccess.projectStages.description')}
      />
      {stages.data === undefined ? (
        stages.error === null ? (
          <LoadingState />
        ) : (
          <ErrorState
            error={stages.error}
            message={problemMessage(stages.error, t)}
            onRetry={stages.reload}
          />
        )
      ) : (
        <>
          {notice !== null && <p className="notice notice--success">{notice}</p>}
          <StagesEditor
            key={stages.data.latest?.id ?? 'none'}
            loaded={stages.data}
            onSaved={(message) => {
              setNotice(message);
              stages.reload();
            }}
          />
        </>
      )}
    </>
  );
}

function StagesEditor({
  loaded,
  onSaved,
}: {
  loaded: Loaded;
  onSaved: (notice: string) => void;
}): ReactElement {
  const { t, language } = useI18n();
  const mayManage = useCan('CONFIGURATION_MANAGE');
  const [rows, setRows] = useState<StageRow[]>(() => rowsOf(loaded.latest));
  const [summary, setSummary] = useState('');
  const [effective, setEffective] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const total = totalOf(rows) / 100;
  const pending =
    loaded.latest !== null && loaded.latest.lifecycleState !== 'PUBLISHED' ? loaded.latest : null;

  const change = useCallback((index: number, next: Partial<StageRow>) => {
    setRows((current) => current.map((row, at) => (at === index ? { ...row, ...next } : row)));
  }, []);
  const move = (index: number, by: number) => {
    setRows((current) => {
      const next = [...current];
      const [row] = next.splice(index, 1);
      if (row !== undefined) {
        next.splice(index + by, 0, row);
      }
      return next;
    });
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setProblem(null);
    if (rows.some((row) => row.name.ar.trim() === '' || row.name.en.trim() === '')) {
      setProblem(t('identityAccess.projectStages.namesRequired'));
      return;
    }
    if (total !== WEIGHT_TOTAL) {
      setProblem(t('identityAccess.projectStages.mustTotal', { total: String(total) }));
      return;
    }
    setSaving(true);
    try {
      const changeSummary = summary.trim() === '' ? null : { text: summary.trim(), language };
      const content = {
        projectStages: rows.map((row, index) => ({
          code: row.code,
          name: { ar: row.name.ar.trim(), en: row.name.en.trim() },
          sortOrder: index + 1,
          weightPercent: row.isActive ? Number(row.weightPercent || 0) : 0,
          isActive: row.isActive,
        })),
      };
      let id = pending?.id;
      let etag: string | null = null;
      if (id === undefined) {
        const created = await apiRequest<{ id: string }>(VERSIONS, {
          method: 'POST',
          body: {
            familyId: loaded.familyId,
            basedOnVersionId: null,
            changeSummary,
          },
        });
        id = created.data.id;
        etag = created.etag;
      } else {
        etag = (await apiRequest<unknown>(`${VERSIONS}/${id}`)).etag;
      }
      if (pending === null || pending.lifecycleState === 'DRAFT') {
        const written = await apiRequest<unknown>(`${VERSIONS}/${id}`, {
          method: 'PUT',
          ifMatch: etag,
          body: { changeSummary, content },
        });
        etag = written.etag;
      }
      if (pending === null || pending.lifecycleState === 'DRAFT') {
        const validated = await apiRequest<unknown>(`${VERSIONS}/${id}/validate`, {
          method: 'POST',
          ifMatch: etag,
        });
        etag = validated.etag;
      }
      await apiRequest<unknown>(`${VERSIONS}/${id}/publish`, {
        method: 'POST',
        ifMatch: etag,
        body: { effectiveFrom: effective === '' ? null : new Date(effective).toISOString() },
      });
      onSaved(t('identityAccess.projectStages.published'));
    } catch (error) {
      if (error instanceof ApiError && error.code === 'MASTER_DATA_CONFIG_SEPARATION_OF_DUTIES') {
        onSaved(t('identityAccess.projectStages.needsAnother'));
      } else {
        setProblem(problemMessage(error, t));
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={problem} />
      <TableContainer caption={t('identityAccess.projectStages.title')}>
        <thead>
          <tr>
            <th scope="col">{t('identityAccess.projectStages.order')}</th>
            <th scope="col">{t('identityAccess.projectStages.nameAr')}</th>
            <th scope="col">{t('identityAccess.projectStages.nameEn')}</th>
            <th scope="col">{t('identityAccess.projectStages.weight')}</th>
            <th scope="col">{t('identityAccess.projectStages.active')}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={row.code}>
              <td>
                <button
                  type="button"
                  className="button button--quiet"
                  disabled={!mayManage || index === 0}
                  aria-label={t('identityAccess.projectStages.moveUp', {
                    name: row.name[language],
                  })}
                  onClick={() => {
                    move(index, -1);
                  }}
                >
                  ↑
                </button>
                <button
                  type="button"
                  className="button button--quiet"
                  disabled={!mayManage || index === rows.length - 1}
                  aria-label={t('identityAccess.projectStages.moveDown', {
                    name: row.name[language],
                  })}
                  onClick={() => {
                    move(index, 1);
                  }}
                >
                  ↓
                </button>
              </td>
              <td>
                <TextField
                  label={t('identityAccess.projectStages.nameAr')}
                  name={`nameAr-${row.code}`}
                  value={row.name.ar}
                  disabled={!mayManage}
                  dir="rtl"
                  onChange={(ar) => {
                    change(index, { name: { ...row.name, ar } });
                  }}
                />
              </td>
              <td>
                <TextField
                  label={t('identityAccess.projectStages.nameEn')}
                  name={`nameEn-${row.code}`}
                  value={row.name.en}
                  disabled={!mayManage}
                  dir="ltr"
                  onChange={(en) => {
                    change(index, { name: { ...row.name, en } });
                  }}
                />
              </td>
              <td>
                <AmountField
                  label={t('identityAccess.projectStages.weight')}
                  name={`weight-${row.code}`}
                  value={row.isActive ? row.weightPercent : '0'}
                  disabled={!mayManage || !row.isActive}
                  onChange={(weightPercent) => {
                    change(index, { weightPercent });
                  }}
                />
              </td>
              <td>
                <input
                  type="checkbox"
                  aria-label={t('identityAccess.projectStages.activeFor', {
                    name: row.name[language],
                  })}
                  checked={row.isActive}
                  disabled={!mayManage}
                  onChange={(event) => {
                    change(index, { isActive: event.target.checked });
                  }}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
      <p className={total === WEIGHT_TOTAL ? 'form__note' : 'field__error'} role="status">
        {t('identityAccess.projectStages.total', { total: String(total) })}
      </p>
      {mayManage && (
        <>
          <button
            type="button"
            className="button"
            onClick={() => {
              setRows((current) => [
                ...current,
                {
                  code: nextCode(current),
                  name: { ar: '', en: '' },
                  weightPercent: '0',
                  isActive: true,
                },
              ]);
            }}
          >
            {t('identityAccess.projectStages.add')}
          </button>
          <TextField
            label={t('identityAccess.projectStages.effective')}
            name="effectiveFrom"
            type="datetime-local"
            value={effective}
            hint={t('identityAccess.projectStages.effectiveHint')}
            onChange={setEffective}
          />
          <TextField
            label={t('identityAccess.projectStages.summary')}
            name="changeSummary"
            value={summary}
            onChange={setSummary}
          />
          <div className="form__actions">
            <button type="submit" className="button button--primary" disabled={saving}>
              {saving ? t('common.states.saving') : t('identityAccess.projectStages.save')}
            </button>
          </div>
        </>
      )}
      <h2>{t('identityAccess.projectStages.history')}</h2>
      <TableContainer caption={t('identityAccess.projectStages.history')}>
        <thead>
          <tr>
            <th scope="col">{t('identityAccess.projectStages.version')}</th>
            <th scope="col">{t('identityAccess.projectStages.state')}</th>
            <th scope="col">{t('identityAccess.projectStages.effective')}</th>
          </tr>
        </thead>
        <tbody>
          {loaded.versions.map((version) => (
            <tr key={version.id}>
              <td dir="ltr">{version.versionNo}</td>
              <td>{version.lifecycleState}</td>
              <td dir="ltr">{version.effectiveFrom ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
    </form>
  );
}
