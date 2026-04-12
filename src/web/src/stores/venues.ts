import { defineStore } from 'pinia'
import { ref } from 'vue'
import { venuesApi, type Venue, type CreateVenueRequest, type UpdateVenueRequest } from '../services/venues'

export const useVenuesStore = defineStore('venues', () => {
  const venues = ref<Venue[]>([])
  const isLoading = ref(false)
  const continuationToken = ref<string | undefined>()

  async function loadVenues(type?: string, reset = false) {
    if (reset) {
      venues.value = []
      continuationToken.value = undefined
    }
    isLoading.value = true
    try {
      const { data } = await venuesApi.list(type, continuationToken.value)
      if (reset) {
        venues.value = data.items
      } else {
        venues.value.push(...data.items)
      }
      continuationToken.value = data.continuationToken
    } finally {
      isLoading.value = false
    }
  }

  async function createVenue(request: CreateVenueRequest): Promise<Venue> {
    const { data } = await venuesApi.create(request)
    venues.value.unshift(data)
    return data
  }

  async function updateVenue(id: string, request: UpdateVenueRequest): Promise<Venue> {
    const { data } = await venuesApi.update(id, request)
    const idx = venues.value.findIndex(v => v.id === id)
    if (idx >= 0) venues.value[idx] = data
    return data
  }

  async function deleteVenue(id: string) {
    await venuesApi.delete(id)
    venues.value = venues.value.filter(v => v.id !== id)
  }

  async function createVenueFromUrl(url: string): Promise<Venue> {
    const { data } = await venuesApi.createFromUrl(url)
    venues.value.unshift(data)
    return data
  }

  return {
    venues,
    isLoading,
    continuationToken,
    loadVenues,
    createVenue,
    updateVenue,
    deleteVenue,
    createVenueFromUrl,
  }
})
