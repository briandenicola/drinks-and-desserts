import { defineStore } from 'pinia'
import { ref } from 'vue'
import { itemsApi, type Item, type UpdateItemRequest, type CreateWishlistRequest } from '../services/items'

export const useItemsStore = defineStore('items', () => {
  const items = ref<Item[]>([])
  const isLoading = ref(false)
  const continuationToken = ref<string | undefined>()
  const currentType = ref<string | undefined>()
  const currentSortBy = ref<string | undefined>()
  const currentSortDirection = ref<'asc' | 'desc'>('desc')
  const currentGroupBy = ref<string | undefined>()
  const hasInitialLoad = ref(false)
  const scrollPosition = ref(0)

  const wishlistItems = ref<Item[]>([])
  const isLoadingWishlist = ref(false)
  const wishlistContinuationToken = ref<string | undefined>()
  const wishlistType = ref<string | undefined>()
  const wishlistSortBy = ref<string | undefined>()
  const wishlistSortDirection = ref<'asc' | 'desc'>('desc')
  const wishlistGroupBy = ref<string | undefined>()
  const wishlistHasInitialLoad = ref(false)
  const wishlistScrollPosition = ref(0)

  const activeTab = ref<'collection' | 'wishlist'>('collection')
  const activeFilter = ref<string | undefined>(undefined)
  const searchQuery = ref('')
  const viewSort = ref<string | undefined>(undefined)
  const viewSortDirection = ref<'asc' | 'desc'>('desc')
  const viewGroupBy = ref<string | undefined>(undefined)

  async function loadItems(
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

    if (skipIfLoaded && hasInitialLoad.value && items.value.length > 0 && !criteriaChanged) {
      return
    }

    if (reset || criteriaChanged) {
      items.value = []
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
      const response = await itemsApi.list(
        type,
        continuationToken.value,
        undefined,
        currentSortBy.value,
        currentSortDirection.value,
        currentGroupBy.value
      )
      if (reset || criteriaChanged) {
        items.value = response.data.items
      } else {
        items.value.push(...response.data.items)
      }
      continuationToken.value = response.data.continuationToken ?? undefined
      hasInitialLoad.value = true
    } finally {
      isLoading.value = false
    }
  }

  async function loadWishlist(
    type?: string,
    reset = false,
    sortBy?: string,
    sortDirection?: 'asc' | 'desc',
    groupBy?: string,
    skipIfLoaded = false
  ) {
    const criteriaChanged = type !== wishlistType.value ||
                           sortBy !== wishlistSortBy.value ||
                           sortDirection !== wishlistSortDirection.value ||
                           groupBy !== wishlistGroupBy.value

    if (skipIfLoaded && wishlistHasInitialLoad.value && wishlistItems.value.length > 0 && !criteriaChanged) {
      return
    }

    if (reset || criteriaChanged) {
      wishlistItems.value = []
      wishlistContinuationToken.value = undefined
      wishlistType.value = type
      wishlistSortBy.value = sortBy
      wishlistSortDirection.value = sortDirection ?? 'desc'
      wishlistGroupBy.value = groupBy
      wishlistHasInitialLoad.value = false
      wishlistScrollPosition.value = 0
    }

    isLoadingWishlist.value = true
    try {
      const response = await itemsApi.list(
        type,
        wishlistContinuationToken.value,
        'wishlist',
        wishlistSortBy.value,
        wishlistSortDirection.value,
        wishlistGroupBy.value
      )
      if (reset || criteriaChanged) {
        wishlistItems.value = response.data.items
      } else {
        wishlistItems.value.push(...response.data.items)
      }
      wishlistContinuationToken.value = response.data.continuationToken ?? undefined
      wishlistHasInitialLoad.value = true
    } finally {
      isLoadingWishlist.value = false
    }
  }

  async function updateItem(id: string, data: UpdateItemRequest) {
    const response = await itemsApi.update(id, data)
    const index = items.value.findIndex(i => i.id === id)
    if (index !== -1) {
      items.value[index] = response.data
    }
    return response.data
  }

  async function deleteItem(id: string) {
    await itemsApi.delete(id)
    items.value = items.value.filter(i => i.id !== id)
    wishlistItems.value = wishlistItems.value.filter(i => i.id !== id)
  }

  async function createWishlistItem(data: CreateWishlistRequest) {
    const response = await itemsApi.createWishlistItem(data)
    wishlistItems.value.unshift(response.data)
    return response.data
  }

  async function convertWishlistItem(id: string) {
    const response = await itemsApi.convertWishlistItem(id)
    wishlistItems.value = wishlistItems.value.filter(i => i.id !== id)
    items.value.unshift(response.data)
    return response.data
  }

  async function createWishlistFromUrl(url: string) {
    const response = await itemsApi.extractWishlistFromUrl(url)
    wishlistItems.value.unshift(response.data)
    return response.data
  }

  return {
    items, isLoading, continuationToken, currentType, currentSortBy, currentSortDirection, currentGroupBy, hasInitialLoad, scrollPosition, loadItems,
    wishlistItems, isLoadingWishlist, wishlistContinuationToken, wishlistType, wishlistSortBy, wishlistSortDirection, wishlistGroupBy, wishlistHasInitialLoad, wishlistScrollPosition, loadWishlist,
    updateItem, deleteItem,
    createWishlistItem, convertWishlistItem,
    createWishlistFromUrl,
    activeTab, activeFilter, searchQuery, viewSort, viewSortDirection, viewGroupBy,
  }
})
