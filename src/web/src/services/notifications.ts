import api from './api'

export interface AppNotification {
  id: string
  userId: string
  type: 'friend-request' | 'friend-accepted' | 'new-thought'
  title: string
  detail?: string
  sourceUserId: string
  sourceDisplayName: string
  referenceType?: string
  referenceId?: string
  isRead: boolean
  createdAt: string
}

export interface NotificationsResponse {
  notifications: AppNotification[]
  unreadCount: number
}

export const notificationsApi = {
  list: (limit = 50) => api.get<NotificationsResponse>('/api/notifications', { params: { limit } }),
  markRead: (id: string) => api.put(`/api/notifications/${id}/read`),
  markAllRead: () => api.put('/api/notifications/read-all'),
}
