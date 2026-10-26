import { type ReactElement, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { dashboardsApi } from './api/dashboardsApi.ts';
import { type DashboardView } from './api/types.ts';
import { arrangedWidgets } from './layout.ts';
import { dashboardProblemMessage } from './problems.ts';

interface Choice {
  widgetCode: string;
  title: string;
  isHidden: boolean;
}

/**
 * ADR-019 on the Portfolio Dashboard: show, hide and reorder its optional widgets, or go back to the governed layout.
 * A choice changes how a widget is shown, never what it presents or who may see it (DSH-CC-29); the governed widgets
 * are not offered. Saved as a whole, numbered in the order shown.
 */
function PersonalizeForm({
  view,
  onClose,
  onSaved,
}: {
  view: DashboardView;
  onClose: () => void;
  onSaved: (reset: boolean) => void;
}): ReactElement {
  const { t, language } = useI18n();
  const [choices, setChoices] = useState<Choice[]>(() =>
    arrangedWidgets(view.widgets)
      .filter((widget) => widget.isOptionalVisibility)
      .map((widget) => ({
        widgetCode: widget.code,
        title: widget.title[language],
        isHidden: widget.isHidden,
      })),
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const move = (index: number, by: -1 | 1) => {
    setChoices((current) => {
      const next = [...current];
      const [moved] = next.splice(index, 1);
      if (moved !== undefined) {
        next.splice(index + by, 0, moved);
      }
      return next;
    });
  };

  const run = async (reset: boolean) => {
    setBusy(true);
    setError(null);
    try {
      if (reset) {
        await dashboardsApi.resetPersonalization(view.code);
      } else {
        await dashboardsApi.personalize(
          view.code,
          choices.map((choice, index) => ({
            widgetCode: choice.widgetCode,
            isHidden: choice.isHidden,
            sortOrder: index + 1,
          })),
        );
      }
      onSaved(reset);
    } catch (failure) {
      setError(dashboardProblemMessage(failure, t));
      setBusy(false);
    }
  };

  return (
    <form
      className="form"
      onSubmit={(event) => {
        event.preventDefault();
        void run(false);
      }}
    >
      <p className="form__note">{t('dashboards.personalize.intro')}</p>
      <FormAlert message={error} />
      <ol className="personalize__list">
        {choices.map((choice, index) => (
          <li key={choice.widgetCode} className="personalize__item">
            <label className="field__option">
              <input
                type="checkbox"
                checked={!choice.isHidden}
                onChange={(event) => {
                  const shown = event.target.checked;
                  setChoices((current) =>
                    current.map((candidate) =>
                      candidate.widgetCode === choice.widgetCode
                        ? { ...candidate, isHidden: !shown }
                        : candidate,
                    ),
                  );
                }}
              />
              {choice.title}
            </label>
            <span className="personalize__moves">
              <button
                type="button"
                className="button button--quiet"
                disabled={index === 0}
                onClick={() => {
                  move(index, -1);
                }}
              >
                {t('dashboards.personalize.moveUp')}{' '}
                <span className="visually-hidden">{choice.title}</span>
              </button>
              <button
                type="button"
                className="button button--quiet"
                disabled={index === choices.length - 1}
                onClick={() => {
                  move(index, 1);
                }}
              >
                {t('dashboards.personalize.moveDown')}{' '}
                <span className="visually-hidden">{choice.title}</span>
              </button>
            </span>
          </li>
        ))}
      </ol>
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={busy}>
          {t('dashboards.personalize.save')}
        </button>
        <button
          type="button"
          className="button"
          disabled={busy}
          onClick={() => {
            void run(true);
          }}
        >
          {t('dashboards.personalize.reset')}
        </button>
        <button type="button" className="button button--quiet" disabled={busy} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}

export function PersonalizeDialog({
  open,
  view,
  onClose,
  onSaved,
}: {
  open: boolean;
  view: DashboardView;
  onClose: () => void;
  onSaved: (reset: boolean) => void;
}): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={open} title={t('dashboards.personalize.title')} onClose={onClose}>
      <PersonalizeForm view={view} onClose={onClose} onSaved={onSaved} />
    </Dialog>
  );
}
