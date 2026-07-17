import api from './api'

export interface ListQueryOptions {
  search?: string
  pageSize?: number
}

const normalizePageSize = (pageSize?: number) =>
  typeof pageSize === 'number' ? Math.min(Math.max(Math.floor(pageSize), 1), 100) : undefined


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
    venueId?: string
    name: string
    address?: string
    placeId?: string
  }
  photoUrls: string[]
  aiConfidence?: number
  aiSummary?: string
  userRating?: number
  userNotes?: string
  sourceAttribution?: SourceAttribution
  journal?: JournalEntry[]
  tags: string[]
  status: string
  processedBy?: string
  createdAt: string
  updatedAt: string
}

export interface SourceAttribution {
  sourceUserId: string
  sourceDisplayName: string
  sourceItemId: string
  addedAt: string
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
    venueId?: string
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
  list: (
    type?: string,
    continuationToken?: string,
    status?: string,
    sortBy?: string,
    sortDirection?: 'asc' | 'desc',
    groupBy?: string,
    options?: ListQueryOptions
  ) =>
    api.get<{ items: Item[]; continuationToken?: string; hasMore: boolean }>(
      '/items', {
        params: {
          type,
          continuationToken,
          status,
          sortBy,
          sortDirection,
          groupBy,
          search: options?.search?.trim() || undefined,
          pageSize: normalizePageSize(options?.pageSize),
        },
      }
    ),

  get: (id: string) =>
    api.get<Item>(`/items/${id}`),

  update: (id: string, data: UpdateItemRequest) =>
    api.put<Item>(`/items/${id}`, data),

  delete: (id: string) =>
    api.delete(`/items/${id}`),

  createWishlistItem: (data: CreateWishlistRequest) =>
    api.post<Item>('/items/wishlist', data),

  extractWishlistFromUrl: (url: string) =>
    api.post<Item>('/items/wishlist/from-url', { url }),

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

  share: (id: string, friendId: string) =>
    api.post(`/items/${id}/share`, { friendId }),
}
