import api from './api'

export interface AppNotification {
  id: string
  userId: string
  type: 'friend-request' | 'friend-accepted' | 'new-thought' | 'workflow-completed'
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
  list: (limit = 50) => api.get<NotificationsResponse>('/notifications', { params: { limit } }),
  unreadCount: () => api.get<{ unreadCount: number }>('/notifications/unread-count'),
  markRead: (id: string) => api.put(`/notifications/${id}/read`),
  markAllRead: () => api.put('/notifications/read-all'),
}
