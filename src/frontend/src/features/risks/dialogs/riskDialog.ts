import { type RiskActionCommand, type RiskCommand } from '../api/risksApi.ts';

/** The risk modal open on SCR-082, one at a time; each acts on the risk the screen shows. */
export type RiskDialog =
  | { kind: 'edit' } // MOD-031 Edit Risk
  | { kind: 'assess' } // MOD-032 Risk Assessment
  | { kind: 'owner' } // MOD-033 Assign Risk Owner
  | { kind: 'action'; actionId: string | null } // MOD-034 Add Mitigation / Response, or edit one
  | { kind: 'close' } // MOD-035 Close Risk
  | { kind: 'accept' } // Accepting the risk until an expiry (TASK-055 D-7)
  | { kind: 'command'; command: RiskCommand }
  | { kind: 'actionCommand'; actionId: string; command: RiskActionCommand };
