import { screen } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import {
  CLASSIFICATION_ID,
  DEPARTMENT_ID,
  ENTITY_ID,
  entitySession,
  ETAG,
  LIGHT_PROFILE_ID,
  MAKKAH_REGION_ID,
  projectDetail,
  PROJECT_ID,
  reviewerSession,
  RIYADH_CITY_ID,
  RIYADH_REGION_ID,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-033 Create Project and SCR-034 Edit Project Draft. Acceptance criterion 1: required fields are checked in the
// browser and by the API before submission is enabled.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

function openCreate(session: Session = reviewerSession(), lookups = {}): MockApi {
  const api = withProjectLookups(mockApi(), lookups).on('POST', /^\/projects$/, (request) => ({
    status: 201,
    body: projectDetail({ ...(request.body as object), id: PROJECT_ID }),
    headers: { ETag: ETAG },
  }));
  withProject(api, projectDetail());
  renderApp({ path: '/projects/new', session });
  return api;
}

const createButton = () => screen.getByRole('button', { name: en('projects.create.submit') });

async function fillRequired(user: UserEvent) {
  await user.type(screen.getByLabelText(/Project title/), 'Coastal road upgrade');
  await user.selectOptions(await screen.findByLabelText(/Classification/), CLASSIFICATION_ID);
  await user.selectOptions(screen.getByLabelText(/Governance profile/), LIGHT_PROFILE_ID);
  await user.click(screen.getByLabelText(en('projects.participationMode.AHDA_MANAGED')));
  await user.selectOptions(screen.getByLabelText(/Owning department/), DEPARTMENT_ID);
}

describe('SCR-033 Create Project', () => {
  test('saving is disabled until every required field is filled, and the note says what is missing', async () => {
    const user = userEvent.setup();
    const api = openCreate();
    await screen.findByRole('option', { name: 'Infrastructure' });

    expect(createButton()).toHaveProperty('disabled', true);
    const note = document.getElementById(createButton().getAttribute('aria-describedby') ?? '');
    expect(note?.textContent).toContain('Project title');
    expect(note?.textContent).toContain('Owning department');

    await user.type(screen.getByLabelText(/Project title/), 'Coastal road upgrade');
    expect(createButton()).toHaveProperty('disabled', true);
    expect(note?.textContent).not.toContain('Project title');

    await fillRequired(user);
    await user.clear(screen.getByLabelText(/Project title/));
    await user.type(screen.getByLabelText(/Project title/), 'Coastal road upgrade');
    expect(createButton()).toHaveProperty('disabled', false);
    expect(api.requestsTo('POST', /^\/projects$/)).toHaveLength(0);
  });

  test('an entity-managed project needs its entity before it can be saved', async () => {
    const user = userEvent.setup();
    openCreate();
    await fillRequired(user);
    expect(createButton()).toHaveProperty('disabled', false);

    await user.click(screen.getByLabelText(en('projects.participationMode.ENTITY_MANAGED')));
    expect(createButton()).toHaveProperty('disabled', true);

    await user.selectOptions(screen.getByLabelText(/External entity/), ENTITY_ID);
    expect(createButton()).toHaveProperty('disabled', false);
  });

  test('values of the wrong shape are flagged as typed and keep saving disabled', async () => {
    const user = userEvent.setup();
    openCreate();
    await fillRequired(user);

    await user.type(screen.getByLabelText(/Registration budget/), '1,500,000');
    expect(screen.getByLabelText(/Registration budget/).getAttribute('aria-invalid')).toBe('true');
    expect(createButton()).toHaveProperty('disabled', true);
    await user.clear(screen.getByLabelText(/Registration budget/));
    await user.type(screen.getByLabelText(/Registration budget/), '1500000.5');
    expect(screen.getByLabelText(/Registration budget/).getAttribute('aria-invalid')).toBeNull();

    await user.type(screen.getByLabelText(/Planned start date/), '2026-12-01');
    await user.type(screen.getByLabelText(/Planned end date/), '2026-11-01');
    expect(screen.getByText(en('identityAccess.fieldErrors.dateBeforeStart'))).toBeTruthy();
    expect(createButton()).toHaveProperty('disabled', true);

    await user.clear(screen.getByLabelText(/Planned end date/));
    await user.type(screen.getByLabelText(/Latitude/), '95');
    expect(createButton()).toHaveProperty('disabled', true);
  });

  test('sends the registration as the API reads it, then opens the new draft', async () => {
    const user = userEvent.setup();
    const api = openCreate();
    await fillRequired(user);
    await user.type(screen.getByLabelText(/Registration budget/), '1500000.5');
    await user.selectOptions(screen.getByLabelText(/Region/), RIYADH_REGION_ID);
    await user.selectOptions(screen.getByLabelText(/City/), RIYADH_CITY_ID);
    await user.type(screen.getByLabelText(/Latitude/), '24.7136');

    await user.click(createButton());

    expect(await screen.findByText(en('projects.done.created'))).toBeTruthy();
    const [request] = api.requestsTo('POST', /^\/projects$/);
    expect(request?.headers.get('Idempotency-Key')).not.toBeNull();
    expect(request?.body).toEqual({
      title: { text: 'Coastal road upgrade', language: 'en' },
      description: null,
      classificationItemId: CLASSIFICATION_ID,
      departmentId: DEPARTMENT_ID,
      externalEntityId: null,
      participationMode: 'AHDA_MANAGED',
      governanceProfileItemId: LIGHT_PROFILE_ID,
      registrationBudgetSar: '1500000.50',
      plannedStartDate: null,
      plannedEndDate: null,
      regionItemId: RIYADH_REGION_ID,
      cityItemId: RIYADH_CITY_ID,
      latitude: 24.7136,
      longitude: null,
    });
    // No Formal Project ID is ever sent: AHDA issues it (ADR-013).
    expect(request?.body).not.toHaveProperty('formalProjectId');
  });

  test('a city is offered only within the chosen region, and a region change clears one outside it', async () => {
    const user = userEvent.setup();
    openCreate();
    await screen.findByRole('option', { name: 'Riyadh Region' });

    await user.selectOptions(screen.getByLabelText(/Region/), RIYADH_REGION_ID);
    await user.selectOptions(screen.getByLabelText(/City/), RIYADH_CITY_ID);
    expect(screen.queryByRole('option', { name: 'Jeddah' })).toBeNull();

    await user.selectOptions(screen.getByLabelText(/Region/), MAKKAH_REGION_ID);
    expect(screen.getByLabelText(/City/)).toHaveProperty('value', '');
    expect(screen.getByRole('option', { name: 'Jeddah' })).toBeTruthy();
  });

  test('the API’s refusals are shown: a field error on its field, a rule on the form', async () => {
    const user = userEvent.setup();
    const api = openCreate();
    api.on(
      'POST',
      /^\/projects$/,
      problem(400, 'VALIDATION_FAILED', [{ field: 'title.text', code: 'MAX_LENGTH' }]),
    );
    await fillRequired(user);
    await user.click(createButton());

    expect(await screen.findByText(en('common.problems.validationFailed'))).toBeTruthy();
    expect(screen.getByLabelText(/Project title/).getAttribute('aria-invalid')).toBe('true');
    expect(screen.getByText(en('common.fieldErrors.maxLength'))).toBeTruthy();

    api.on('POST', /^\/projects$/, problem(422, 'PROJECT_REFERENCE_INVALID'));
    await user.click(createButton());
    expect(await screen.findByText(en('projects.problems.referenceInvalid'))).toBeTruthy();
  });

  test('an external entity user registers for their own entity (ADR-013 as amended)', async () => {
    const user = userEvent.setup();
    const api = openCreate(entitySession());
    await user.type(screen.getByLabelText(/Project title/), 'Entity depot');
    await user.selectOptions(await screen.findByLabelText(/Classification/), CLASSIFICATION_ID);
    await user.selectOptions(screen.getByLabelText(/Governance profile/), LIGHT_PROFILE_ID);
    await user.selectOptions(screen.getByLabelText(/Owning department/), DEPARTMENT_ID);

    // The entity is theirs and fixed; the mode starts as entity-managed.
    expect(screen.queryByRole('combobox', { name: /External entity/ })).toBeNull();
    expect(screen.getByText('Build Co')).toBeTruthy();
    expect(screen.getByLabelText(en('projects.participationMode.ENTITY_MANAGED'))).toHaveProperty(
      'checked',
      true,
    );

    await user.click(createButton());
    await screen.findByText(en('projects.done.created'));
    expect(api.requestsTo('POST', /^\/projects$/)[0]?.body).toMatchObject({
      externalEntityId: ENTITY_ID,
      participationMode: 'ENTITY_MANAGED',
    });
  });

  test('a person who cannot read the lists is told why, and cannot save', async () => {
    openCreate(entitySession(), { catalogues: false, organization: false });

    expect(await screen.findByText(en('projects.form.unreadable.title'))).toBeTruthy();
    expect(screen.getByText(en('projects.form.unreadable.catalogues'))).toBeTruthy();
    expect(screen.getByText(en('projects.form.unreadable.organization'))).toBeTruthy();
    expect(createButton()).toHaveProperty('disabled', true);
  });

  test('has no axe violation', async () => {
    openCreate();
    await screen.findByRole('option', { name: 'Infrastructure' });
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });
});

describe('SCR-034 Edit Project Draft', () => {
  test('starts from the stored registration and replaces it under its ETag', async () => {
    const user = userEvent.setup();
    const api = withProject(withProjectLookups(mockApi()), projectDetail());
    api.on('PUT', new RegExp(`^/projects/${PROJECT_ID}$`), {
      body: projectDetail(),
      headers: { ETag: '"12"' },
    });
    renderApp({ path: `/projects/${PROJECT_ID}/edit`, session: entitySession() });

    const title = await screen.findByLabelText(/Project title/);
    expect(title).toHaveProperty('value', 'Coastal road upgrade');
    await user.clear(screen.getByLabelText(/Registration budget/));
    await user.type(screen.getByLabelText(/Registration budget/), '2000000');
    await user.click(screen.getByRole('button', { name: en('common.actions.save') }));

    expect(await screen.findByText(en('projects.done.updated'))).toBeTruthy();
    const [request] = api.requestsTo('PUT', /^\/projects\//);
    expect(request?.headers.get('If-Match')).toBe(ETAG);
    expect(request?.body).toMatchObject({
      registrationBudgetSar: '2000000.00',
      // Unchanged text keeps the language it was entered in.
      title: { text: 'Coastal road upgrade', language: 'en' },
    });
  });

  test('a project past review is not editable here', async () => {
    withProject(withProjectLookups(mockApi()), projectDetail({ status: 'UNDER_REVIEW' }));
    renderApp({ path: `/projects/${PROJECT_ID}/edit`, session: entitySession() });

    expect(
      await screen.findByText(
        en('projects.edit.notEditable', { status: en('projects.status.UNDER_REVIEW') }),
      ),
    ).toBeTruthy();
    expect(screen.queryByLabelText(/Project title/)).toBeNull();
  });

  test('a change made meanwhile is refused (412) and explained', async () => {
    const user = userEvent.setup();
    const api = withProject(withProjectLookups(mockApi()), projectDetail());
    api.on('PUT', /^\/projects\//, problem(412, 'PRECONDITION_FAILED'));
    renderApp({ path: `/projects/${PROJECT_ID}/edit`, session: entitySession() });

    await user.click(await screen.findByRole('button', { name: en('common.actions.save') }));
    expect(await screen.findByText(en('common.problems.preconditionFailed'))).toBeTruthy();
  });
});
