import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import axios from 'axios'
import {
  authApi,
  storeAuth,
  clearAuth,
  getStoredToken,
  getStoredRefreshToken,
  getStoredExpiresAt,
  getStoredUser,
  refreshAccessToken,
  type RegisterRequest,
  type LoginRequest,
} from '../services/auth'
import { usersApi, type User } from '../services/users'
import { loginWithEntra, logoutEntra, isEntraConfigured } from '../services/msal'
import { getErrorMessage } from '../services/errors'
import router from '../router'

const REFRESH_BUFFER_MS = 5 * 60_000 // refresh 5 minutes before expiry

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const token = ref<string | null>(getStoredToken())
  const isLoading = ref(true)
  const error = ref<string | null>(null)

  let refreshTimer: ReturnType<typeof setTimeout> | null = null

  const isAuthenticated = computed(() => !!token.value)
  const isAdmin = computed(() => user.value?.role === 'admin')

  function scheduleRefresh() {
    if (refreshTimer) {
      clearTimeout(refreshTimer)
      refreshTimer = null
    }

    const expiresAt = getStoredExpiresAt()
    if (!expiresAt) return

    const expiresMs = new Date(expiresAt).getTime()
    const delay = expiresMs - Date.now() - REFRESH_BUFFER_MS

    if (delay <= 0) {
      performRefresh()
      return
    }

    refreshTimer = setTimeout(() => performRefresh(), delay)
  }

  // Best-effort proactive refresh — never forces logout on failure.
  // If it fails, the 401 interceptor will handle it on the next API call.
  async function performRefresh() {
    const result = await refreshAccessToken()

    if (result) {
      token.value = result.token
      user.value = result.user
      scheduleRefresh()
    } else if (getStoredToken()) {
      // Transient failure but auth wasn't cleared — retry in 30s
      refreshTimer = setTimeout(() => performRefresh(), 30_000)
    }
    // If auth was cleared (definitive 401), do nothing — interceptor handles redirect
  }

  function handleVisibilityChange() {
    if (document.visibilityState !== 'visible' || !token.value) return

    // Sync reactive state with localStorage (another tab may have refreshed)
    const storedToken = getStoredToken()
    if (storedToken && storedToken !== token.value) {
      token.value = storedToken
      const storedUser = getStoredUser()
      if (storedUser) user.value = storedUser
    }

    const expiresAt = getStoredExpiresAt()
    if (!expiresAt) return

    const expiresMs = new Date(expiresAt).getTime()
    if (Date.now() >= expiresMs - REFRESH_BUFFER_MS) {
      performRefresh()
    } else {
      scheduleRefresh()
    }
  }

  // Sync token changes from other tabs via storage events
  function handleStorageChange(e: StorageEvent) {
    if (e.key === 'whiskey_and_smokes_token') {
      if (e.newValue) {
        token.value = e.newValue
        const storedUser = getStoredUser()
        if (storedUser) user.value = storedUser
        scheduleRefresh()
      } else {
        // Another tab logged out
        token.value = null
        user.value = null
        router.push('/login')
      }
    }
  }

  function initialize() {
    const storedUser = getStoredUser()
    if (storedUser && token.value) {
      user.value = storedUser
      loadUser()
      scheduleRefresh()
    }
    isLoading.value = false

    document.addEventListener('visibilitychange', handleVisibilityChange)
    window.addEventListener('storage', handleStorageChange)
  }

  function dispose() {
    document.removeEventListener('visibilitychange', handleVisibilityChange)
    window.removeEventListener('storage', handleStorageChange)
    if (refreshTimer) {
      clearTimeout(refreshTimer)
      refreshTimer = null
    }
  }

  function applyAuthResponse(data: {
    token: string
    refreshToken: string
    expiresAt: string
    user: User
  }) {
    storeAuth(data)
    token.value = data.token
    user.value = data.user
    scheduleRefresh()
  }

  async function register(data: RegisterRequest) {
    error.value = null
    try {
      const response = await authApi.register(data)
      applyAuthResponse(response.data)
      router.push('/')
    } catch (e: unknown) {
      error.value = getErrorMessage(e, 'Registration failed')
      throw e
    }
  }

  async function login(data: LoginRequest) {
    error.value = null
    try {
      const response = await authApi.login(data)
      applyAuthResponse(response.data)
      router.push('/')
    } catch (e: unknown) {
      error.value = getErrorMessage(e, 'Login failed')
      throw e
    }
  }

  async function loginEntra() {
    error.value = null
    try {
      const msalResult = await loginWithEntra()
      const response = await authApi.entraLogin(msalResult.accessToken)
      applyAuthResponse(response.data)
      router.push('/')
    } catch (e: unknown) {
      error.value = getErrorMessage(e, 'Microsoft sign-in failed')
      throw e
    }
  }

  async function logout() {
    // Revoke refresh token on the server
    const refreshToken = getStoredRefreshToken()
    const accessToken = getStoredToken()
    if (accessToken && refreshToken) {
      try {
        await axios.post(
          '/api/auth/logout',
          { refreshToken },
          { headers: { Authorization: `Bearer ${accessToken}` } }
        )
      } catch {
        // Best-effort server logout
      }
    }

    if (isEntraConfigured()) {
      try {
        await logoutEntra()
      } catch {
        // Entra logout is best-effort
      }
    }

    if (refreshTimer) {
      clearTimeout(refreshTimer)
      refreshTimer = null
    }

    clearAuth()
    token.value = null
    user.value = null
    router.push('/login')
  }

  async function loadUser() {
    try {
      const response = await usersApi.getMe()
      user.value = response.data
    } catch (e) {
      console.error('Failed to load user', e)
    }
  }

  return { user, isLoading, error, isAuthenticated, isAdmin, initialize, dispose, applyAuthResponse, register, login, loginEntra, logout, loadUser }
})
