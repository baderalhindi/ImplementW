import { Fragment, type ReactElement, type ReactNode } from 'react';
import { Link, useParams } from 'react-router';

import {
  FAQ_PERMISSIONS,
  type GuideArticleMeta,
  GUIDE_ARTICLES,
  helpContent,
  isForRoles,
  LABEL_REFERENCE,
} from '@/content/help/help.ts';
import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type TranslationKey } from '@/shared/i18n/resources.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';

import { NotFoundPage } from '../pages.tsx';

// Blueprint §20.1 menu 11, Help & Knowledge (TASK-096): read-only pages over the bundled content in
// src/content/help. Nothing here calls the API or offers an action; the screens a guide names keep their own checks.

/** Text with its `[[key]]` references shown as the interface labels they name, in the reader's language. */
function HelpText({ text }: { text: string }): ReactElement {
  const { t } = useI18n();
  const parts: ReactNode[] = [];
  let last = 0;
  for (const match of text.matchAll(LABEL_REFERENCE)) {
    parts.push(text.slice(last, match.index));
    parts.push(
      <strong key={match.index} className="help-label">
        {t((match[1] ?? '') as TranslationKey)}
      </strong>,
    );
    last = match.index + match[0].length;
  }
  parts.push(text.slice(last));
  return <>{parts}</>;
}

/**
 * The articles written for the reader's own roles, and only those (13 Nov 2026 review): the guide speaks to its reader, never about
 * the others' work. An article for another role reads as not found, as for an external user before (ADR-013).
 */
function useGuideArticles(): GuideArticleMeta[] {
  const { session } = useSession();
  const roleCodes = session?.user.roleAssignments.map((assignment) => assignment.roleCode) ?? [];
  const { can, loaded } = usePermissions();
  return GUIDE_ARTICLES.filter(
    (article) =>
      (article.permission === undefined || !loaded || can(article.permission)) &&
      isForRoles(article, roleCodes),
  );
}

function ArticleList({ articles }: { articles: GuideArticleMeta[] }): ReactElement {
  const { language } = useI18n();
  return (
    <ul className="help-list">
      {articles.map((article) => {
        const content = helpContent[language].guide[article.id];
        return (
          content !== undefined && (
            <li key={article.id}>
              <Link to={`/help/guide/${article.id}`}>{content.title}</Link>
              <p>
                <HelpText text={content.summary} />
              </p>
            </li>
          )
        );
      })}
    </ul>
  );
}

/** SCR-011: the User Guide's index, the reader's own role first. */
export function UserGuidePage(): ReactElement {
  const { t } = useI18n();
  const articles = useGuideArticles();
  return (
    <>
      <PageHeader
        title={t('common.help.guide.title')}
        description={t('common.help.guide.description')}
      />
      <section className="section" aria-labelledby="help-for-you">
        <h2 id="help-for-you">{t('common.help.guide.forYou')}</h2>
        <ArticleList articles={articles} />
      </section>
    </>
  );
}

/** SCR-011: one guide article. An id the reader is not offered reads as not found, as an unknown one does. */
export function UserGuideArticlePage(): ReactElement {
  const { t, language } = useI18n();
  const { articleId } = useParams();
  const meta = useGuideArticles().find((article) => article.id === articleId);
  const content = meta === undefined ? undefined : helpContent[language].guide[meta.id];
  if (meta === undefined || content === undefined) {
    return <NotFoundPage />;
  }
  return (
    <>
      <p>
        <Link to="/help/guide">{t('common.help.guide.back')}</Link>
      </p>
      <PageHeader title={content.title} />
      <p className="help-summary">
        <HelpText text={content.summary} />
      </p>
      {content.sections.map((section, index) => (
        <section key={index} className="section help-article__section">
          <h2>{section.heading}</h2>
          {section.paragraphs?.map((paragraph, at) => (
            <p key={at}>
              <HelpText text={paragraph} />
            </p>
          ))}
          {section.steps !== undefined && (
            <ol className="help-steps">
              {section.steps.map((step, at) => (
                <li key={at}>
                  <HelpText text={step} />
                </li>
              ))}
            </ol>
          )}
          {section.note !== undefined && (
            <p className="notice notice--warning">
              <HelpText text={section.note} />
            </p>
          )}
        </section>
      ))}
      {meta.related.length > 0 && (
        <section className="section" aria-labelledby="help-related">
          <h2 id="help-related">{t('common.help.guide.related')}</h2>
          <ul className="link-list">
            {meta.related.map((item) => (
              <li key={item.to}>
                <Link to={item.to}>{t(item.label as TranslationKey)}</Link>
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  );
}

/** SCR-012 and SCR-126: frequently asked questions, grouped; each answer opens in place. */
export function FaqPage(): ReactElement {
  const { t, language } = useI18n();
  const { can, loaded } = usePermissions();
  // An entry only a holder of its permission would ask is left out for everyone else; a group left empty, too.
  const groups = Object.entries(helpContent[language].faq)
    .map(([groupId, group]) => {
      const entries = Object.entries(group.entries).filter(([entryId]) => {
        const permission = FAQ_PERMISSIONS[entryId];
        return permission === undefined || !loaded || can(permission);
      });
      return { groupId, heading: group.heading, entries };
    })
    .filter((group) => group.entries.length > 0);
  return (
    <>
      <PageHeader
        title={t('common.help.faq.title')}
        description={t('common.help.faq.description')}
      />
      {groups.map(({ groupId, heading, entries }) => (
        <section key={groupId} className="section" aria-labelledby={`faq-${groupId}`}>
          <h2 id={`faq-${groupId}`}>{heading}</h2>
          {entries.map(([entryId, entry]) => (
            <details key={entryId} className="help-faq">
              <summary>
                <HelpText text={entry.question} />
              </summary>
              {entry.answer.map((paragraph, at) => (
                <p key={at}>
                  <HelpText text={paragraph} />
                </p>
              ))}
            </details>
          ))}
        </section>
      ))}
    </>
  );
}

/** SCR-125: the terms the screens use, defined once. */
export function KnowledgeBasePage(): ReactElement {
  const { t, language } = useI18n();
  // Sorted as the reader sees the terms: a term that names a label sorts by that label in their language.
  const shown = (text: string) =>
    text.replace(LABEL_REFERENCE, (_, key: string) => t(key as TranslationKey));
  const terms = Object.entries(helpContent[language].knowledgeBase).sort(([, a], [, b]) =>
    shown(a.term).localeCompare(shown(b.term), language),
  );
  return (
    <>
      <PageHeader
        title={t('common.help.knowledgeBase.title')}
        description={t('common.help.knowledgeBase.description')}
      />
      <dl className="help-glossary">
        {terms.map(([termId, entry]) => (
          <Fragment key={termId}>
            <dt>
              <HelpText text={entry.term} />
            </dt>
            <dd>
              <HelpText text={entry.definition} />
            </dd>
          </Fragment>
        ))}
      </dl>
    </>
  );
}
