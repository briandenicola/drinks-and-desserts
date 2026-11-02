import { computed, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { usersApi } from '../services/users'
import { getErrorMessage } from '../services/errors'
import { isThemeId, type ThemeId } from '../services/theme'

export function useThemeSelection() {
  const auth = useAuthStore()
  const isSaving = ref(false)
  const message = ref('')
  const isError = ref(false)
  const pendingTheme = ref<ThemeId | null>(null)
  // Identifies the latest request so a superseded one cannot touch shared state
  let requestSeq = 0

  const selectedTheme = computed<ThemeId | null>(() => {
    const theme = auth.user?.preferences?.theme
    return isThemeId(theme) ? theme : null
  })

  // What the radios show: the in-flight choice, reverting to the saved theme on failure
  const displayedTheme = computed(() => pendingTheme.value ?? selectedTheme.value)

  function resetState() {
    requestSeq++
    isSaving.value = false
    message.value = ''
    isError.value = false
    pendingTheme.value = null
  }

  // A different (or no) account must not inherit another account's pending save
  watch(() => auth.user?.id, resetState)

  async function selectTheme(id: ThemeId) {
    if (!auth.user || isSaving.value || id === selectedTheme.value) return

    const accountId = auth.user.id
    const seq = ++requestSeq
    const stillCurrent = () => seq === requestSeq && auth.user?.id === accountId

    isSaving.value = true
    pendingTheme.value = id
    message.value = ''
    isError.value = false
    try {
      // The server replaces the whole preferences object, so send it complete
      const response = await usersApi.updateMe({
        preferences: { ...auth.user.preferences, theme: id },
      })
      if (!stillCurrent()) return
      auth.setUser(response.data)
      message.value = 'Theme saved'
    } catch (e: unknown) {
      if (!stillCurrent()) return
      message.value = getErrorMessage(e, 'Failed to save theme')
      isError.value = true
    } finally {
      if (seq === requestSeq) {
        isSaving.value = false
        pendingTheme.value = null
      }
    }
  }

  return { selectedTheme, displayedTheme, selectTheme, isSaving, message, isError }
}
