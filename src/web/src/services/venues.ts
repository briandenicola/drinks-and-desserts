import api from './api'

export interface VenueWorkflowStep {
  stepId: string
  agentName: string
  status: 'pending' | 'running' | 'complete' | 'error'
  summary?: string
  detail?: string
  timestamp: string
}

export interface Venue {
  id: string
  userId: string
  name: string
  address?: string
  website?: string
  type: string
  rating?: number
  photoUrls: string[]
  labels: string[]
  location?: { latitude: number; longitude: number }
  placeId?: string
  status: string
  workflowSteps: VenueWorkflowStep[]
  processingError?: string
  createdAt: string
  updatedAt: string
}

export interface CreateVenueRequest {
  name: string
  address?: string
  website?: string
  type: string
  rating?: number
  location?: { latitude: number; longitude: number }
  labels?: string[]
}

export interface UpdateVenueRequest {
  name?: string
  address?: string
  website?: string
  type?: string
  rating?: number
  location?: { latitude: number; longitude: number }
  labels?: string[]
}

export const venuesApi = {
  list: (type?: string, continuationToken?: string) =>
    api.get<{ items: Venue[]; continuationToken?: string; hasMore: boolean }>(
      '/venues', { params: { type, continuationToken } }
    ),

  get: (id: string) =>
    api.get<Venue>(`/venues/${id}`),

  create: (data: CreateVenueRequest) =>
    api.post<Venue>('/venues', data),

  update: (id: string, data: UpdateVenueRequest) =>
    api.put<Venue>(`/venues/${id}`, data),

  delete: (id: string) =>
    api.delete(`/venues/${id}`),

  getItems: (id: string, continuationToken?: string) =>
    api.get<{ items: unknown[]; continuationToken?: string; hasMore: boolean }>(
      `/venues/${id}/items`, { params: { continuationToken } }
    ),

  getPhotoUploadUrl: (id: string, fileName: string) =>
    api.get<{ uploadUrl: string; blobUrl: string }>(`/venues/${id}/photos/upload-url`, { params: { fileName } }),

  addPhoto: (id: string, blobUrl: string) =>
    api.post<Venue>(`/venues/${id}/photos`, { blobUrl }),

  removePhoto: (id: string, blobUrl: string) =>
    api.delete<Venue>(`/venues/${id}/photos`, { data: { blobUrl } }),

  createFromUrl: (url: string) =>
    api.post<Venue>('/venues/from-url', { url }),
}
