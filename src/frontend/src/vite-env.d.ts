/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** TASK-090: the environment tag the APM files this build's errors under; error reporting is on only when it is set. */
  readonly VITE_APM_ENVIRONMENT_TAG?: string;
}
