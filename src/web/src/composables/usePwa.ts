import { computed } from 'vue'

/**
 * Detects if the app is running in PWA mode (installed or display: standalone)
 */
export function usePwa() {
  const isPwa = computed(() => {
    // Check if app is in standalone mode (iOS) or display-mode (Android/Desktop)
    return (
      window.matchMedia('(display-mode: standalone)').matches ||
      (window.navigator as Navigator & { standalone?: boolean }).standalone === true
    )
  })

  return { isPwa }
}
