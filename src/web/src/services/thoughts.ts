import api from './api'

export interface Thought {
  id: string
  authorId: string
  authorDisplayName: string
  targetUserId: string
  targetType: 'item' | 'venue'
  targetId: string
  targetName: string
  content: string
  rating?: number
  createdAt: string
  updatedAt: string
}

export interface CreateThoughtRequest {
  content: string
  targetUserId: string
  targetType: 'item' | 'venue'
  targetId: string
  rating?: number
}

export interface UpdateThoughtRequest {
  content: string
  rating?: number
}

export const thoughtsApi = {
  getForTarget: (targetType: string, targetId: string, targetUserId: string) =>
    api.get<Thought[]>(`/api/thoughts/${targetType}/${targetId}`, { params: { targetUserId } }),
  create: (data: CreateThoughtRequest) => api.post<Thought>('/api/thoughts', data),
  update: (id: string, data: UpdateThoughtRequest) => api.put<Thought>(`/api/thoughts/${id}`, data),
  remove: (id: string) => api.delete(`/api/thoughts/${id}`),
  mine: () => api.get<Thought[]>('/api/thoughts/mine'),
  onMyItems: () => api.get<Thought[]>('/api/thoughts/on-my-items'),
}
