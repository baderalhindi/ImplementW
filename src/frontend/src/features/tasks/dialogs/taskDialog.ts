import { type TaskDependencyDetail } from '../api/types.ts';

/**
 * The task modal open on a screen, one at a time. A modal opened from a task's MOD-012 names that task (`taskId`, or
 * the subtask's `parentId`) and returns to it when it closes.
 */
export type TaskDialog =
  | { kind: 'create' } // MOD-010
  | { kind: 'edit'; taskId: string; fromRow?: true } // MOD-011; from a register's row it closes back to the list
  | { kind: 'detail'; taskId: string } // MOD-012
  | { kind: 'subtask'; parentId: string } // MOD-013
  | { kind: 'assign'; taskId: string } // MOD-014
  | { kind: 'dependency'; taskId: string | null } // MOD-015
  | { kind: 'removeDependency'; dependency: TaskDependencyDetail; taskId: string | null };

/** The MOD-012 a modal returns to, or null when it was opened from the screen itself. */
export function returnTo(dialog: TaskDialog): string | null {
  switch (dialog.kind) {
    case 'create':
    case 'detail':
      return null;
    case 'subtask':
      return dialog.parentId;
    case 'edit':
      return dialog.fromRow === true ? null : dialog.taskId;
    case 'assign':
    case 'dependency':
    case 'removeDependency':
      return dialog.taskId;
  }
}
