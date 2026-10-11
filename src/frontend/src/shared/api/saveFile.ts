import { type DownloadedFile } from './httpClient.ts';

/** Hands the bytes to the browser as a file to save; the object URL is released once the click is dispatched. */
export function saveFile({ content, fileName }: DownloadedFile, fallbackName: string): void {
  const url = URL.createObjectURL(content);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName ?? fallbackName;
  anchor.rel = 'noopener';
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}
