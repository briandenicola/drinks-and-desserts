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
  type RegisterRequest,
  type LoginRequest,
  type AuthResponse,
} from '../services/auth'
import { usersApi, type User } from '../services/users'
import { loginWithEntra, logoutEntra, isEntraConfigured } from '../services/msal'
import router from '../router'

const REFRESH_BUFFER_MS = 60_000 // refresh 1 minute before expiry

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

  async function performRefresh() {
    const accessToken = getStoredToken()
    const refreshToken = getStoredRefreshToken()

    if (!accessToken || !refreshToken) return

    try {
      const { data } = await axios.post<AuthResponse>('/api/auth/refresh', {
        accessToken,
        refreshToken,
      })

      storeAuth(data)
      token.value = data.token
      user.value = data.user
      scheduleRefresh()
    } catch {
      // Refresh failed — the 401 interceptor will handle redirect on next API call
    }
  }

  function handleVisibilityChange() {
    if (document.visibilityState !== 'visible' || !token.value) return

    const expiresAt = getStoredExpiresAt()
    if (!expiresAt) return

    const expiresMs = new Date(expiresAt).getTime()
    if (Date.now() >= expiresMs - REFRESH_BUFFER_MS) {
      performRefresh()
    } else {
      scheduleRefresh()
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
  }

  function dispose() {
    document.removeEventListener('visibilitychange', handleVisibilityChange)
    if (refreshTimer) {
      clearTimeout(refreshTimer)
      refreshTimer = null
    }
  }

  async function register(data: RegisterRequest) {
    error.value = null
    try {
      const response = await authApi.register(data)
      storeAuth(response.data)
      token.value = response.data.token
      user.value = response.data.user
      scheduleRefresh()
      router.push('/')
    } catch (e: any) {
      error.value = e.response?.data?.message ?? 'Registration failed'
      throw e
    }
  }

  async function login(data: LoginRequest) {
    error.value = null
    try {
      const response = await authApi.login(data)
      storeAuth(response.data)
      token.value = response.data.token
      user.value = response.data.user
      scheduleRefresh()
      router.push('/')
    } catch (e: any) {
      error.value = e.response?.data?.message ?? 'Login failed'
      throw e
    }
  }

  async function loginEntra() {
    error.value = null
    try {
      const msalResult = await loginWithEntra()
      const response = await authApi.entraLogin(msalResult.accessToken)
      storeAuth(response.data)
      token.value = response.data.token
      user.value = response.data.user
      scheduleRefresh()
      router.push('/')
    } catch (e: any) {
      error.value = e.response?.data?.message ?? e.message ?? 'Microsoft sign-in failed'
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

  return { user, isLoading, error, isAuthenticated, isAdmin, initialize, dispose, register, login, loginEntra, logout, loadUser }
})
