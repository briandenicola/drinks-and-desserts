import api from './api'

export interface User {
  id: string
  displayName: string
  email: string
  role: string
  authProvider: string
  preferences: {
    favoriteTypes: string[]
    defaultView: string
  }
  createdAt: string
  updatedAt: string
}

export interface Prompt {
  id: string
  name: string
  description: string
  content: string
  updatedAt: string
  updatedBy: string | null
}

export interface LoggingSettings {
  defaultLevel: string
  categoryLevels: Record<string, string>
}

export interface LoggingSettingsResponse {
  settings: LoggingSettings
  availableCategories: Record<string, string>
  availableLevels: string[]
}

export interface FoundryStatus {
  endpoint: string
  projectEndpoint: string
  visionModel: string
  reasoningModel: string
  isEndpointConfigured: boolean
  isProjectConfigured: boolean
  agentValidation: {
    status: string
    foundAgents: string[]
    missingAgents: string[]
    error: string | null
  }
  connectivityTest: {
    status: string
    message: string
    latencyMs: number | null
    testedAt: string
  } | null
  checkedAt: string
}

export const usersApi = {
  getMe: () => api.get<User>('/users/me'),

  updateMe: (data: { displayName?: string; preferences?: User['preferences'] }) =>
    api.put<User>('/users/me', data),

  // Admin - Users
  listUsers: () => api.get<User[]>('/admin/users'),

  updateRole: (userId: string, role: string) =>
    api.put<User>(`/admin/users/${userId}/role`, { role }),

  resetPassword: (userId: string, newPassword: string) =>
    api.put(`/admin/users/${userId}/password`, { newPassword }),

  deleteUser: (userId: string) =>
    api.delete(`/admin/users/${userId}`),

  // Admin - Prompts
  listPrompts: () => api.get<Prompt[]>('/admin/prompts'),

  updatePrompt: (id: string, content: string) =>
    api.put<Prompt>(`/admin/prompts/${id}`, { content }),

  // Admin - Logging
  getLoggingSettings: () => api.get<LoggingSettingsResponse>('/admin/logging'),

  updateLoggingSettings: (settings: LoggingSettings) =>
    api.put<LoggingSettings>('/admin/logging', settings),

  changePassword: (data: { currentPassword: string; newPassword: string }) =>
    api.put('/users/me/password', data),

  exportData: () =>
    api.get('/users/me/export', { responseType: 'blob' }),

  getStats: () =>
    api.get('/users/me/stats'),

  // Admin - Foundry
  getFoundryStatus: () => api.get<FoundryStatus>('/admin/foundry'),

  testFoundryConnectivity: () => api.post<FoundryStatus>('/admin/foundry/test'),
}
