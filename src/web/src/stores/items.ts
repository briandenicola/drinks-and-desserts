import { defineStore } from 'pinia'
import { ref } from 'vue'
import { itemsApi, type Item, type UpdateItemRequest, type CreateWishlistRequest } from '../services/items'

export const useItemsStore = defineStore('items', () => {
  const items = ref<Item[]>([])
  const isLoading = ref(false)
  const continuationToken = ref<string | undefined>()

  const wishlistItems = ref<Item[]>([])
  const isLoadingWishlist = ref(false)
  const wishlistContinuationToken = ref<string | undefined>()

  const activeTab = ref<'collection' | 'wishlist'>('collection')
  const activeFilter = ref<string | undefined>(undefined)

  async function loadItems(type?: string, reset = false) {
    if (reset) {
      items.value = []
      continuationToken.value = undefined
    }

    isLoading.value = true
    try {
      const response = await itemsApi.list(type, continuationToken.value)
      items.value.push(...response.data.items)
      continuationToken.value = response.data.continuationToken ?? undefined
    } finally {
      isLoading.value = false
    }
  }

  async function loadWishlist(type?: string, reset = false) {
    if (reset) {
      wishlistItems.value = []
      wishlistContinuationToken.value = undefined
    }

    isLoadingWishlist.value = true
    try {
      const response = await itemsApi.list(type, wishlistContinuationToken.value, 'wishlist')
      wishlistItems.value.push(...response.data.items)
      wishlistContinuationToken.value = response.data.continuationToken ?? undefined
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
    items, isLoading, loadItems,
    wishlistItems, isLoadingWishlist, loadWishlist,
    updateItem, deleteItem,
    createWishlistItem, convertWishlistItem,
    createWishlistFromUrl,
    activeTab, activeFilter,
  }
})
