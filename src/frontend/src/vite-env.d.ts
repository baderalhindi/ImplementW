/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** TASK-090: the environment tag the APM files this build's errors under; error reporting is on only when it is set. */
  readonly VITE_APM_ENVIRONMENT_TAG?: string;
  /** The browser key for Google's Map Tiles API. Unset, the projects map draws its circles on a plain background. */
  readonly VITE_GOOGLE_MAPS_API_KEY?: string;
  /** AHDA's own password reset page; the directory owns passwords, so the sign-in page only links to it. */
  readonly VITE_PASSWORD_RESET_URL?: string;
}
