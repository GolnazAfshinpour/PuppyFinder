import { onMounted, onUnmounted } from 'vue'

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), '
  + 'textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

/**
 * The five things every dialog in this app owes the reader.
 *
 * Escape closes it; the page behind stops scrolling; focus moves into it on open; Tab cycles
 * inside it rather than escaping into the page behind (which is never aria-hidden, so a
 * screen-reader user who tabbed out landed in content the overlay visually covers); and focus
 * returns to whatever opened it on close, so dismissing a dialog doesn't dump keyboard users
 * back at the top of the document.
 *
 * DogDetail did the first three; the other dialogs did none — so Escape closed the dog detail
 * and silently did nothing elsewhere, which is worse than a consistent absence because it
 * teaches the key and then ignores it. Extracted rather than copied so there is one
 * implementation to be right.
 *
 * @param {() => void} close          what the dialog does when dismissed
 * @param {import('vue').Ref=} focusTarget element to focus on open (usually the close button);
 *                                    falls back to the container's first focusable element
 * @param {import('vue').Ref=} container the dialog box element, for the focus trap — without
 *                                    it Tab behaves as before (no trap), never worse
 */
export function useModal(close, focusTarget, container) {
  let opener = null

  const focusables = () =>
    container?.value
      ? [...container.value.querySelectorAll(FOCUSABLE)]
          .filter((el) => el.getClientRects().length > 0)
      : []

  function onKeydown(event) {
    if (event.key === 'Escape') {
      close()
      return
    }

    if (event.key !== 'Tab' || !container?.value) return
    const items = focusables()
    if (items.length === 0) return

    const first = items[0]
    const last = items[items.length - 1]
    const inside = container.value.contains(document.activeElement)
    if (event.shiftKey && (document.activeElement === first || !inside)) {
      event.preventDefault()
      last.focus()
    } else if (!event.shiftKey && (document.activeElement === last || !inside)) {
      event.preventDefault()
      first.focus()
    }
  }

  onMounted(() => {
    opener = document.activeElement
    document.addEventListener('keydown', onKeydown)
    // Keep the page behind from scrolling under the dialog.
    document.body.style.overflow = 'hidden'
    ;(focusTarget?.value ?? focusables()[0])?.focus()
  })

  onUnmounted(() => {
    document.removeEventListener('keydown', onKeydown)
    document.body.style.overflow = ''
    // The opener can have left the DOM (a card that closed with its dog) — optional call.
    opener?.focus?.()
  })
}
