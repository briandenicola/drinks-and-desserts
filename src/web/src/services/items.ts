import api from './api'

export interface Item {
  id: string
  userId: string
  captureId: string
  type: string
  name: string
  brand?: string
  category?: string
  details?: Record<string, unknown>
  venue?: {
    name: string
    address?: string
    placeId?: string
  }
  photoUrls: string[]
  aiConfidence?: number
  aiSummary?: string
  userRating?: number
  userNotes?: string
  journal?: JournalEntry[]
  tags: string[]
  status: string
  createdAt: string
  updatedAt: string
}

export interface JournalEntry {
  text: string
  date: string
  source?: string
}

export interface UpdateItemRequest {
  name?: string
  type?: string
  brand?: string
  category?: string
  venue?: {
    name: string
    address?: string
  }
  userRating?: number
  userNotes?: string
  journalEntry?: string
  tags?: string[]
  status?: string
}

export interface CreateWishlistRequest {
  name: string
  type: string
  brand?: string
  notes?: string
  venueName?: string
  tags?: string[]
}

export const itemsApi = {
  list: (type?: string, continuationToken?: string, status?: string) =>
    api.get<{ items: Item[]; continuationToken?: string; hasMore: boolean }>(
      '/items', { params: { type, continuationToken, status } }
    ),

  get: (id: string) =>
    api.get<Item>(`/items/${id}`),

  update: (id: string, data: UpdateItemRequest) =>
    api.put<Item>(`/items/${id}`, data),

  delete: (id: string) =>
    api.delete(`/items/${id}`),

  createWishlistItem: (data: CreateWishlistRequest) =>
    api.post<Item>('/items/wishlist', data),

  convertWishlistItem: (id: string) =>
    api.post<Item>(`/items/${id}/convert`),

  getSuggestions: () =>
    api.get<{ names: string[]; brands: string[]; tags: string[] }>('/items/suggestions'),

  getPhotoUploadUrl: (id: string, fileName: string) =>
    api.get<{ uploadUrl: string; blobUrl: string }>(`/items/${id}/photos/upload-url`, { params: { fileName } }),

  addPhoto: (id: string, blobUrl: string) =>
    api.post<Item>(`/items/${id}/photos`, { blobUrl }),

  removePhoto: (id: string, blobUrl: string) =>
    api.delete<Item>(`/items/${id}/photos`, { data: { blobUrl } }),
}
