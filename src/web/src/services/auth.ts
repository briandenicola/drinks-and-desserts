import api from './api'
import type { User } from './users'

export interface RegisterRequest {
  email: string
  password: string
  displayName: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface AuthResponse {
  token: string
  refreshToken: string
  expiresAt: string
  user: User
}

const TOKEN_KEY = 'whiskey_and_smokes_token'
const REFRESH_TOKEN_KEY = 'whiskey_and_smokes_refresh_token'
const EXPIRES_AT_KEY = 'whiskey_and_smokes_expires_at'
const USER_KEY = 'whiskey_and_smokes_user'

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function getStoredRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY)
}

export function getStoredExpiresAt(): string | null {
  return localStorage.getItem(EXPIRES_AT_KEY)
}

export function getStoredUser(): User | null {
  const data = localStorage.getItem(USER_KEY)
  if (!data) return null
  try {
    return JSON.parse(data)
  } catch {
    localStorage.removeItem(USER_KEY)
    return null
  }
}

export function storeAuth(auth: AuthResponse) {
  localStorage.setItem(TOKEN_KEY, auth.token)
  localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken)
  localStorage.setItem(EXPIRES_AT_KEY, auth.expiresAt)
  localStorage.setItem(USER_KEY, JSON.stringify(auth.user))
}

export function clearAuth() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(REFRESH_TOKEN_KEY)
  localStorage.removeItem(EXPIRES_AT_KEY)
  localStorage.removeItem(USER_KEY)
}

export const authApi = {
  register: (data: RegisterRequest) =>
    api.post<AuthResponse>('/auth/register', data),

  login: (data: LoginRequest) =>
    api.post<AuthResponse>('/auth/login', data),

  entraLogin: (accessToken: string) =>
    api.post<AuthResponse>('/auth/entra', { accessToken }),
}
