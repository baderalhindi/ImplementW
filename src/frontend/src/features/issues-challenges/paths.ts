// Where the WF-07 screens live. A concern is under its project's Issues & challenges tab, the address the escalation
// notification links to (TASK-057 D-6); an escalation's detail is under /issues-challenges, readable without the project.

export function concernPath(projectId: string, concernId: string): string {
  return `/projects/${projectId}/issues-challenges/${concernId}`;
}

export function escalationPath(escalationId: string): string {
  return `/issues-challenges/escalations/${escalationId}`;
}
