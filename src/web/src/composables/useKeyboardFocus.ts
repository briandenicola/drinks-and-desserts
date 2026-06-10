import { ref, onMounted, onUnmounted } from 'vue'

// Only text-entry input types that open the virtual keyboard on mobile.
// Excludes checkbox, radio, button, range, color, file, etc.
const INPUT_SELECTORS = [
  'input[type="text"]',
  'input[type="email"]',
  'input[type="password"]',
  'input[type="search"]',
  'input[type="tel"]',
  'input[type="url"]',
  'input[type="number"]',
  'input[type="date"]',
  'input[type="time"]',
  'input[type="datetime-local"]',
  'input[type="month"]',
  'input[type="week"]',
  'input:not([type])',
  'textarea',
  '[contenteditable="true"]',
].join(', ')

/**
 * Tracks whether a text input is currently focused (i.e. the virtual keyboard is likely open).
 * Uses a small debounce so moving focus between two inputs doesn't cause a visible flicker.
 */
export function useKeyboardFocus() {
  const isInputFocused = ref(false)

  let blurTimer: ReturnType<typeof setTimeout> | null = null

  function onFocusIn(event: FocusEvent) {
    const target = event.target as Element | null
    if (target?.matches(INPUT_SELECTORS)) {
      if (blurTimer !== null) {
        clearTimeout(blurTimer)
        blurTimer = null
      }
      isInputFocused.value = true
    }
  }

  function onFocusOut(event: FocusEvent) {
    const target = event.target as Element | null
    if (target?.matches(INPUT_SELECTORS)) {
      // Defer the "not focused" state so that moving focus directly between
      // two inputs doesn't briefly show the toolbar between the blur and
      // the next focus event.
      blurTimer = setTimeout(() => {
        // Double-check: if the new activeElement is also an input, stay hidden
        if (!document.activeElement?.matches(INPUT_SELECTORS)) {
          isInputFocused.value = false
        }
        blurTimer = null
      }, 150)
    }
  }

  onMounted(() => {
    document.addEventListener('focusin', onFocusIn)
    document.addEventListener('focusout', onFocusOut)
  })

  onUnmounted(() => {
    document.removeEventListener('focusin', onFocusIn)
    document.removeEventListener('focusout', onFocusOut)
    if (blurTimer !== null) {
      clearTimeout(blurTimer)
    }
  })

  return { isInputFocused }
}
