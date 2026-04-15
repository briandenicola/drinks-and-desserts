import api from './api'

export interface Friendship {
  id: string
  userId: string
  friendId: string
  friendDisplayName: string
  friendEmail?: string
  status: 'pending-sent' | 'pending-received' | 'accepted' | 'declined' | 'blocked'
  createdAt: string
  updatedAt: string
}

export interface FriendInvite {
  id: string
  userId: string
  userDisplayName: string
  expiresAt: string
  maxUses: number
  usedBy: string[]
  isActive: boolean
  createdAt: string
}

export interface FriendRequests {
  sent: Friendship[]
  received: Friendship[]
}

export const friendsApi = {
  list: () => api.get<Friendship[]>('/api/friends'),
  listRequests: () => api.get<FriendRequests>('/api/friends/requests'),
  createInvite: () => api.post<FriendInvite>('/api/friends/invite'),
  joinViaInvite: (code: string) => api.post<{ friendshipId: string; friendDisplayName: string }>(`/api/friends/join/${code}`),
  accept: (friendshipId: string) => api.put(`/api/friends/${friendshipId}/accept`),
  decline: (friendshipId: string) => api.put(`/api/friends/${friendshipId}/decline`),
  remove: (friendshipId: string) => api.delete(`/api/friends/${friendshipId}`),

  // Browse friend data
  getFriendItems: (friendId: string, continuationToken?: string) =>
    api.get('/api/friends/' + friendId + '/items', { params: { continuationToken } }),
  getFriendItem: (friendId: string, itemId: string) =>
    api.get('/api/friends/' + friendId + '/items/' + itemId),
  getFriendVenues: (friendId: string, continuationToken?: string) =>
    api.get('/api/friends/' + friendId + '/venues', { params: { continuationToken } }),
  getFriendVenue: (friendId: string, venueId: string) =>
    api.get('/api/friends/' + friendId + '/venues/' + venueId),
}
