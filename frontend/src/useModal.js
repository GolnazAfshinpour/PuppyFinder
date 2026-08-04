import { onMounted, onUnmounted } from 'vue'

/**
 * The three things every dialog in this app owes the reader.
 *
 * DogDetail did all three; SafetyGuide, SourcedPrices and BreedQuiz did none — so Escape
 * closed the dog detail and silently did nothing on the other three, which is worse than a
 * consistent absence because it teaches the key and then ignores it. Extracted rather than
 * copied so there is one implementation to be right.
 *
 * @param {() => void} close          what the dialog does when dismissed
 * @param {import('vue').Ref=} focusTarget element to focus on open (usually the close button)
 */
export function useModal(close, focusTarget) {
  function onKeydown(event) {
    if (event.key === 'Escape') close()
  }

  onMounted(() => {
    document.addEventListener('keydown', onKeydown)
    // Keep the page behind from scrolling under the dialog.
    document.body.style.overflow = 'hidden'
    focusTarget?.value?.focus()
  })

  onUnmounted(() => {
    document.removeEventListener('keydown', onKeydown)
    document.body.style.overflow = ''
  })
}
