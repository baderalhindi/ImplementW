import { type DragEvent, type ReactElement, type SyntheticEvent, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { CheckboxField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import {
  MAX_QUICK_ACTIONS,
  QUICK_ACTION_GROUPS,
  type QuickAction,
  type QuickActionContext,
  availableQuickActions,
  defaultQuickActionCodes,
} from './quickActions.ts';

interface QuickActionsPickerProps {
  open: boolean;
  context: QuickActionContext;
  /** The buttons shown now, in order. */
  shown: readonly QuickAction[];
  /** Whether the person still has their role's set. */
  usingDefault: boolean;
  /** Saves the codes in order, or null for the role's set; resolves false when refused. */
  onSave: (actions: string[] | null) => Promise<boolean>;
  onClose: () => void;
}

/**
 * "تخصيص الإجراءات السريعة" (review round 8, point 08): the person ticks up to six of the actions they may do, grouped as open, create and follow,
 * puts them in order by dragging (or with the move buttons, from the keyboard), and saves; "restore default" brings back their role's set.
 */
export function QuickActionsPicker(props: QuickActionsPickerProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('home.quickActions.customize')} onClose={props.onClose}>
      {props.open && <PickerBody {...props} />}
    </Dialog>
  );
}

function PickerBody({
  context,
  shown,
  usingDefault,
  onSave,
  onClose,
}: QuickActionsPickerProps): ReactElement {
  const { t } = useI18n();
  const available = availableQuickActions(context);
  const byCode = new Map(available.map((entry) => [entry.code, entry]));
  const [chosen, setChosen] = useState<string[]>(() => shown.map((entry) => entry.code));
  // The role's set restored and left as it is: saved as "no choice", so the set follows the role.
  const [restored, setRestored] = useState(usingDefault);
  const [dragged, setDragged] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [failed, setFailed] = useState(false);
  const full = chosen.length >= MAX_QUICK_ACTIONS;

  const change = (next: string[]) => {
    setChosen(next);
    setRestored(false);
  };
  const toggle = (code: string) => {
    change(chosen.includes(code) ? chosen.filter((entry) => entry !== code) : [...chosen, code]);
  };
  const move = (from: number, to: number) => {
    if (to < 0 || to >= chosen.length || from === to) {
      return;
    }
    const next = [...chosen];
    const [code] = next.splice(from, 1);
    if (code !== undefined) {
      next.splice(to, 0, code);
      change(next);
    }
  };
  const restore = () => {
    setChosen(defaultQuickActionCodes(context));
    setRestored(true);
  };
  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setSaving(true);
    setFailed(false);
    const saved = await onSave(restored ? null : chosen);
    setSaving(false);
    if (saved) {
      onClose();
    } else {
      setFailed(true);
    }
  };
  const drop = (event: DragEvent, index: number) => {
    event.preventDefault();
    if (dragged !== null) {
      move(dragged, index);
    }
    setDragged(null);
  };

  return (
    <form className="form quick-picker" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={failed ? t('home.quickActions.notSaved') : null} />
      <section aria-labelledby="quick-picker-chosen">
        <div className="quick-picker__header">
          <h3 id="quick-picker-chosen">{t('home.quickActions.chosen')}</h3>
          <span className="quick-picker__count" aria-live="polite">
            {t('home.quickActions.count', {
              count: chosen.length,
              max: MAX_QUICK_ACTIONS,
            })}
          </span>
        </div>
        {chosen.length === 0 ? (
          <p className="form__note">{t('home.quickActions.noneChosen')}</p>
        ) : (
          <ol className="quick-picker__order">
            {chosen.map((code, index) => {
              const entry = byCode.get(code);
              if (entry === undefined) {
                return null;
              }
              const name = t(entry.label);
              return (
                <li
                  key={code}
                  draggable
                  className={dragged === index ? 'is-dragged' : undefined}
                  onDragStart={(event) => {
                    event.dataTransfer.effectAllowed = 'move';
                    event.dataTransfer.setData('text/plain', code);
                    setDragged(index);
                  }}
                  onDragOver={(event) => {
                    event.preventDefault();
                  }}
                  onDrop={(event) => {
                    drop(event, index);
                  }}
                  onDragEnd={() => {
                    setDragged(null);
                  }}
                >
                  <span className="quick-picker__grip" aria-hidden="true">
                    ⋮⋮
                  </span>
                  <span className="quick-picker__name">{name}</span>
                  <button
                    type="button"
                    className="button button--quiet quick-picker__move"
                    aria-label={t('home.quickActions.moveUp', { name })}
                    disabled={index === 0}
                    onClick={() => {
                      move(index, index - 1);
                    }}
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    className="button button--quiet quick-picker__move"
                    aria-label={t('home.quickActions.moveDown', { name })}
                    disabled={index === chosen.length - 1}
                    onClick={() => {
                      move(index, index + 1);
                    }}
                  >
                    ↓
                  </button>
                  <button
                    type="button"
                    className="button button--quiet quick-picker__move"
                    aria-label={t('home.quickActions.remove', { name })}
                    onClick={() => {
                      toggle(code);
                    }}
                  >
                    ×
                  </button>
                </li>
              );
            })}
          </ol>
        )}
      </section>
      {full && <p className="form__note">{t('home.quickActions.full')}</p>}
      {QUICK_ACTION_GROUPS.map((group) => {
        const entries = available.filter((entry) => entry.group === group);
        if (entries.length === 0) {
          return null;
        }
        return (
          <fieldset key={group} className="quick-picker__group">
            <legend>{t(`home.quickActions.groups.${group}`)}</legend>
            {entries.map((entry) => {
              const picked = chosen.includes(entry.code);
              return (
                <CheckboxField
                  key={entry.code}
                  name={`quick-action-${entry.code}`}
                  label={t(entry.label)}
                  checked={picked}
                  disabled={full && !picked}
                  onChange={() => {
                    toggle(entry.code);
                  }}
                />
              );
            })}
          </fieldset>
        );
      })}
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={saving}>
          {t('common.actions.save')}
        </button>
        <button type="button" className="button" disabled={saving} onClick={restore}>
          {t('home.quickActions.restore')}
        </button>
        <button type="button" className="button" disabled={saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
