import { defineStore } from 'pinia'
import { ref } from 'vue'
import { venuesApi, type Venue, type CreateVenueRequest, type UpdateVenueRequest } from '../services/venues'

export const useVenuesStore = defineStore('venues', () => {
  const venues = ref<Venue[]>([])
  const isLoading = ref(false)
  const continuationToken = ref<string | undefined>()
  const currentType = ref<string | undefined>()
  const currentSortBy = ref<string | undefined>()
  const currentSortDirection = ref<'asc' | 'desc'>('desc')
  const currentGroupBy = ref<string | undefined>()
  const hasInitialLoad = ref(false)
  const scrollPosition = ref(0)

  const activeFilter = ref<string | undefined>(undefined)
  const searchQuery = ref('')
  const viewSort = ref<string | undefined>(undefined)
  const viewSortDirection = ref<'asc' | 'desc'>('desc')
  const viewGroupBy = ref<string | undefined>(undefined)

  async function loadVenues(
    type?: string,
    reset = false,
    sortBy?: string,
    sortDirection?: 'asc' | 'desc',
    groupBy?: string,
    skipIfLoaded = false
  ) {
    const criteriaChanged = type !== currentType.value ||
                           sortBy !== currentSortBy.value ||
                           sortDirection !== currentSortDirection.value ||
                           groupBy !== currentGroupBy.value

    if (skipIfLoaded && hasInitialLoad.value && venues.value.length > 0 && !criteriaChanged) {
      return
    }

    if (reset || criteriaChanged) {
      venues.value = []
      continuationToken.value = undefined
      currentType.value = type
      currentSortBy.value = sortBy
      currentSortDirection.value = sortDirection ?? 'desc'
      currentGroupBy.value = groupBy
      hasInitialLoad.value = false
      scrollPosition.value = 0
    }
    
    isLoading.value = true
    try {
      const { data } = await venuesApi.list(
        type,
        continuationToken.value,
        currentSortBy.value,
        currentSortDirection.value,
        currentGroupBy.value
      )
      if (reset || criteriaChanged) {
        venues.value = data.items
      } else {
        venues.value.push(...data.items)
      }
      continuationToken.value = data.continuationToken
      hasInitialLoad.value = true
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
    currentType,
    currentSortBy,
    currentSortDirection,
    currentGroupBy,
    hasInitialLoad,
    scrollPosition,
    loadVenues,
    createVenue,
    updateVenue,
    deleteVenue,
    createVenueFromUrl,
    activeFilter,
    searchQuery,
    viewSort,
    viewSortDirection,
    viewGroupBy,
  }
})
