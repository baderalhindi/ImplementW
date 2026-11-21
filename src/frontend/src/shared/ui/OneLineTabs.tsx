import { type ReactElement, useRef } from 'react';
import { NavLink } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { useOneLineCount } from './useOneLineCount.ts';
import { usePopover } from './usePopover.ts';

export interface OneLineTab {
  to: string;
  label: string;
  end?: boolean | undefined;
  /** Whether this tab is the open one; it decides what the More button says when the open tab is in it. */
  current: boolean;
}

/**
 * A row of tabs that never runs onto a second line: the tabs that fit stay in the row, the rest go into a "More" drop-down at its end.
 * When the open tab is among them, the button carries its name and is marked, so the row always says where one is.
 */
export function OneLineTabs({
  tabs,
  label,
  className = '',
  listLabel,
}: {
  tabs: OneLineTab[];
  /** The accessible name of the list itself, such as the menu's name. */
  listLabel?: string;
  /** The accessible name of the row of links. */
  label: string;
  /** Extra classes on the nav, beside "tabs one-line-tabs". */
  className?: string;
}): ReactElement {
  const { t } = useI18n();
  const navRef = useRef<HTMLElement>(null);
  const measureRef = useRef<HTMLUListElement>(null);
  const shown = useOneLineCount(
    navRef,
    measureRef,
    tabs.length,
    tabs.map((tab) => tab.label).join('\u0000'),
  );

  const hidden = tabs.slice(shown);
  return (
    <nav ref={navRef} className={`tabs one-line-tabs ${className}`.trim()} aria-label={label}>
      <ul aria-label={listLabel}>
        {tabs.slice(0, shown).map((tab) => (
          <li key={tab.to}>
            <NavLink to={tab.to} end={tab.end ?? false}>
              {tab.label}
            </NavLink>
          </li>
        ))}
        {hidden.length > 0 && (
          <li>
            <MoreTabs tabs={hidden} moreLabel={t('common.actions.more')} />
          </li>
        )}
      </ul>
      {/* Every tab and the More button at their natural width, out of sight, to decide how many fit; the labels are drawn by CSS so the page holds no second copy of them. */}
      <ul ref={measureRef} className="one-line-tabs__measure" aria-hidden="true" inert>
        {tabs.map((tab) => (
          <li key={tab.to}>
            <a
              className={tab.current ? 'one-line-tabs__current' : undefined}
              data-label={tab.label}
            />
          </li>
        ))}
        {/* The More button as wide as it can get: "More", or the open tab's name when that tab is in it. */}
        <li>
          <span className="one-line-tabs__more one-line-tabs__more--current">
            <span className="toggle-label">
              <span data-label={`${t('common.actions.more')} ▾`} />
              <span
                data-label={`${tabs.find((tab) => tab.current)?.label ?? t('common.actions.more')} ▾`}
              />
            </span>
          </span>
        </li>
      </ul>
    </nav>
  );
}

function MoreTabs({ tabs, moreLabel }: { tabs: OneLineTab[]; moreLabel: string }): ReactElement {
  const { open, setOpen, containerRef, buttonRef, menuId } = usePopover();
  const current = tabs.find((tab) => tab.current);
  return (
    <div className="popover" ref={containerRef}>
      <button
        ref={buttonRef}
        type="button"
        className={`one-line-tabs__more${current === undefined ? '' : ' one-line-tabs__more--current'}`}
        aria-expanded={open}
        aria-controls={menuId}
        onClick={() => {
          setOpen((value) => !value);
        }}
      >
        {current?.label ?? moreLabel}
        <span aria-hidden="true">▾</span>
      </button>
      <ul className="popover__menu one-line-tabs__menu" id={menuId} hidden={!open}>
        {tabs.map((tab) => (
          <li key={tab.to}>
            <NavLink
              className="popover__option"
              to={tab.to}
              end={tab.end ?? false}
              onClick={() => {
                setOpen(false);
              }}
            >
              {tab.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </div>
  );
}
