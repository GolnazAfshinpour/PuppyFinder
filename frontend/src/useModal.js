import { onMounted, onUnmounted, watch } from 'vue'

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
 * One implementation, two lifecycles: useModal for dialogs that mount when they open
 * (v-if components), useDrawer for panels that stay mounted and toggle open (the mobile
 * filter sheet). Extracted rather than copied so there is one implementation to be right.
 */
function createTrap(close, focusTarget, container) {
  let opener = null
  let active = false

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

  function activate() {
    if (active) return
    active = true
    opener = document.activeElement
    document.addEventListener('keydown', onKeydown)
    // Keep the page behind from scrolling under the dialog.
    document.body.style.overflow = 'hidden'
    ;(focusTarget?.value ?? focusables()[0])?.focus()
  }

  function deactivate() {
    if (!active) return
    active = false
    document.removeEventListener('keydown', onKeydown)
    document.body.style.overflow = ''
    // The opener can have left the DOM (a card that closed with its dog) — optional call.
    opener?.focus?.()
  }

  return { activate, deactivate }
}

/**
 * @param {() => void} close          what the dialog does when dismissed
 * @param {import('vue').Ref=} focusTarget element to focus on open (usually the close button);
 *                                    falls back to the container's first focusable element
 * @param {import('vue').Ref=} container the dialog box element, for the focus trap — without
 *                                    it Tab behaves as before (no trap), never worse
 */
export function useModal(close, focusTarget, container) {
  const trap = createTrap(close, focusTarget, container)
  onMounted(trap.activate)
  onUnmounted(trap.deactivate)
}

/**
 * The same contract for an always-mounted panel toggled by a boolean — open engages the
 * trap, close releases it and hands focus back to the toggle button.
 *
 * @param {import('vue').Ref<boolean>} open
 * @param {() => void} close
 * @param {import('vue').Ref=} container
 */
export function useDrawer(open, close, container) {
  const trap = createTrap(close, undefined, container)
  // flush: 'post' — activate must run after Vue applies the classes that reveal the panel,
  // or the focusables filter sees only hidden elements and initial focus lands nowhere.
  watch(open, (value) => (value ? trap.activate() : trap.deactivate()), { flush: 'post' })
  onUnmounted(trap.deactivate)
}
