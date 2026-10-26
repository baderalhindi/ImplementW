/**
 * The milestone modal open on a screen, one at a time. A modal opened from a milestone's MOD-019 names that milestone
 * and returns to it when it closes, saved or not.
 */
export type MilestoneDialog =
  | { kind: 'create' } // MOD-016 Create Milestone
  | { kind: 'edit'; milestoneId: string } // MOD-016 Edit Milestone
  | { kind: 'achievement'; milestoneId: string } // MOD-019 Milestone Achievement
  // MOD-054 Attach, as evidence of the open revision, with the revision's ETag (the attachment changes it) and the
  // evidence types the policy makes mandatory, offered even when the EVIDENCE_TYPE catalogue cannot be read.
  | {
      kind: 'attach';
      milestoneId: string;
      achievementId: string;
      etag: string | null;
      mandatory: string[];
    }
  | { kind: 'upload'; milestoneId: string }; // MOD-050 Upload Document, to attach once it is clean

/** The MOD-019 a modal returns to, or null when it was opened from the screen itself. */
export function returnTo(dialog: MilestoneDialog): string | null {
  switch (dialog.kind) {
    case 'create':
    case 'achievement':
      return null;
    case 'edit':
    case 'attach':
    case 'upload':
      return dialog.milestoneId;
  }
}
