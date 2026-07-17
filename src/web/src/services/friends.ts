import api from './api'
import type { Item } from './items'
import type { Venue } from './venues'

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
  list: () => api.get<Friendship[]>('/friends'),
  listRequests: () => api.get<FriendRequests>('/friends/requests'),
  createInvite: () => api.post<FriendInvite>('/friends/invite'),
  joinViaInvite: (code: string) => api.post<{ friendshipId: string; friendDisplayName: string }>(`/friends/join/${code}`),
  accept: (friendshipId: string) => api.put(`/friends/${friendshipId}/accept`),
  decline: (friendshipId: string) => api.put(`/friends/${friendshipId}/decline`),
  remove: (friendshipId: string) => api.delete(`/friends/${friendshipId}`),

  // Browse friend data
  getFriendItems: (friendId: string, continuationToken?: string) =>
    api.get('/friends/' + friendId + '/items', { params: { continuationToken } }),
  getFriendItem: (friendId: string, itemId: string) =>
    api.get('/friends/' + friendId + '/items/' + itemId),
  getFriendVenues: (friendId: string, continuationToken?: string) =>
    api.get('/friends/' + friendId + '/venues', { params: { continuationToken } }),
  getFriendVenue: (friendId: string, venueId: string) =>
    api.get('/friends/' + friendId + '/venues/' + venueId),

  // Add from friend's collection
  addFriendItem: (friendId: string, itemId: string) =>
    api.post<Item>('/friends/' + friendId + '/items/' + itemId + '/add'),
  addFriendVenue: (friendId: string, venueId: string) =>
    api.post<Venue>('/friends/' + friendId + '/venues/' + venueId + '/add'),
}
