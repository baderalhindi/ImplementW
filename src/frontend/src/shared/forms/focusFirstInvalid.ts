/**
 * After a save that found fields to complete (review round 8, point 16): moves to the first field marked invalid, in the form's order, once the
 * errors the attempt shows are on screen. `form` is taken from the submit event before any await, since React clears `currentTarget`.
 */
export function focusFirstInvalid(form: EventTarget | null): void {
  if (!(form instanceof HTMLElement)) {
    return;
  }
  requestAnimationFrame(() => {
    const field = form.querySelector<HTMLElement>('[aria-invalid="true"]');
    field?.focus();
    field?.scrollIntoView({ block: 'center' });
  });
}
