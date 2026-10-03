import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, SelectField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { tasksApi } from '../api/tasksApi.ts';
import { type ProjectTaskDetail, type TaskDependencyDetail } from '../api/types.ts';
import { DEPENDENCY_TYPES } from '../presentation.ts';
import { isStale, taskFieldMessage, taskProblemMessage } from '../problems.ts';
import { checkTaskDependency, isLiveLeaf, type TaskDependencyValues } from '../taskRules.ts';

interface TaskDependencyDialogProps {
  tasks: ProjectTaskDetail[];
  dependencies: TaskDependencyDetail[];
  /** Opened from a task's MOD-012: that task is the successor to start with. */
  successorId: string | null;
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}

/**
 * MOD-015 Add Dependency (between tasks): two live leaf tasks (TASK-048 D-6) and how one holds back the other. A link
 * that would close a cycle, one already there, or one the successor has already broken is refused here, before any
 * request; the API refuses them too, for a change made meanwhile.
 */
export function TaskDependencyDialog(props: TaskDependencyDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open title={t('tasks.dependency.title')} onClose={props.onClose}>
      <TaskDependencyForm {...props} />
    </Dialog>
  );
}

function TaskDependencyForm({
  tasks,
  dependencies,
  successorId,
  onClose,
  onDone,
  onStale,
}: TaskDependencyDialogProps): ReactElement {
  const { t } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(taskProblemMessage);
  const [values, setValues] = useState<TaskDependencyValues>({
    predecessorTaskId: '',
    successorTaskId: successorId ?? '',
    dependencyType: 'FS',
  });
  const { codes, cycle } = checkTaskDependency(values, tasks, dependencies);

  const byId = new Map(tasks.map((task) => [task.id, task]));
  const nameOf = (id: string) => byId.get(id)?.title.text ?? shortId(id);
  const chain = cycle === null ? null : [...cycle, cycle[0] ?? ''].map(nameOf).join(' → ');
  const fields = useFieldErrors(
    codes,
    formRef,
    taskFieldMessage,
    chain === null ? {} : { successorTaskId: t('tasks.dependency.circular', { chain }) },
  );

  const leaves = tasks
    .filter(isLiveLeaf)
    .map((task) => ({ value: task.id, label: task.title.text }));
  const set = (field: keyof TaskDependencyValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    fields.clearServer();
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const { dependencyType } = values;
    if (!fields.attempt() || dependencyType === '') {
      return;
    }
    const result = await save.run(() =>
      tasksApi.createDependency({
        predecessorTaskId: values.predecessorTaskId,
        successorTaskId: values.successorTaskId,
        dependencyType,
      }),
    );
    if (result.ok) {
      onDone();
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">{t('tasks.dependency.intro')}</p>
      <FormAlert message={fields.summary ?? save.formError} />
      <SelectField
        label={t('tasks.dependency.predecessor')}
        name="predecessorTaskId"
        required
        value={values.predecessorTaskId}
        options={leaves}
        placeholder={t('common.form.choose')}
        onChange={set('predecessorTaskId')}
        error={fields.errorOf('predecessorTaskId')}
      />
      <SelectField
        label={t('tasks.dependency.successor')}
        name="successorTaskId"
        required
        value={values.successorTaskId}
        options={leaves}
        placeholder={t('common.form.choose')}
        onChange={set('successorTaskId')}
        error={fields.errorOf('successorTaskId')}
      />
      <RadioGroupField
        label={t('tasks.dependency.type')}
        name="dependencyType"
        required
        value={values.dependencyType}
        options={DEPENDENCY_TYPES.map((type) => ({
          value: type,
          label: t(`tasks.dependencyType.${type}`),
        }))}
        onChange={set('dependencyType')}
        error={fields.errorOf('dependencyType')}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving ? t('common.states.saving') : t('tasks.dependency.confirm')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
