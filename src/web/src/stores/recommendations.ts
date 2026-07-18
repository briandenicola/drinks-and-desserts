import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { capturesApi } from '../services/captures'
import {
  recommendationsApi,
  type RecommendedItem,
  type UserRatingProfile,
  type RecommendationRequest,
} from '../services/recommendations'
import { itemsApi } from '../services/items'

export const useRecommendationsStore = defineStore('recommendations', () => {
  // User profile state
  const profileData = ref<UserRatingProfile | null>(null)
  const isLoadingProfile = ref(false)

  // Recommendation request state
  const preferences = ref('')
  const menuPhotoUrl = ref('')
  const selectedTypes = ref<string[]>([])

  // Recommendation response state
  const recommendations = ref<RecommendedItem[]>([])
  const reasoning = ref('')
  const basedOnItems = ref<string[]>([])
  const extractedMenuItems = ref<string[]>([])

  // Loading & error state
  const isLoadingRecommendations = ref(false)
  const isUploadingPhoto = ref(false)
  const isError = ref(false)

  // Wishlist save state
  const savedItems = ref<Set<number>>(new Set())
  const savingItems = ref<Set<number>>(new Set())

  // Thread save state
  const isSavingThread = ref(false)
  const savedThreadId = ref<string | null>(null)
  const lastRequest = ref<RecommendationRequest | null>(null)

  // Computed
  const hasRecommendations = computed(() => recommendations.value.length > 0)

  // Actions
  async function loadUserProfile() {
    if (isLoadingProfile.value) return
    isLoadingProfile.value = true
    try {
      const { data } = await recommendationsApi.getUserProfile()
      profileData.value = data
    } catch (error) {
      console.error('Failed to load user profile:', error)
    } finally {
      isLoadingProfile.value = false
    }
  }

  async function uploadMenuPhoto(photo: File): Promise<string> {
    if (!photo) return menuPhotoUrl.value

    isUploadingPhoto.value = true
    try {
      const { data } = await capturesApi.getUploadUrl(photo.name)

      const headers: Record<string, string> = {
        'Content-Type': photo.type,
      }

      if (data.uploadUrl.includes('blob.core.windows.net') || data.uploadUrl.includes('devstoreaccount')) {
        headers['x-ms-blob-type'] = 'BlockBlob'
      } else {
        const token = localStorage.getItem('whiskey_and_smokes_token')
        if (token) {
          headers['Authorization'] = `Bearer ${token}`
        }
      }

      await fetch(data.uploadUrl, {
        method: 'PUT',
        headers,
        body: photo,
      })

      menuPhotoUrl.value = data.blobUrl
      return data.blobUrl
    } finally {
      isUploadingPhoto.value = false
    }
  }

  async function getRecommendations(photoToUpload?: File) {
    isLoadingRecommendations.value = true
    isError.value = false
    try {
      let photoUrl = menuPhotoUrl.value

      if (photoToUpload) {
        photoUrl = await uploadMenuPhoto(photoToUpload)
      }

      const requestPayload: RecommendationRequest = {
        preferences: preferences.value || undefined,
        menuPhoto: photoUrl || undefined,
        itemTypes: selectedTypes.value.length > 0 ? selectedTypes.value : undefined,
        limit: 5,
      }

      const { data } = await recommendationsApi.getRecommendations(requestPayload)

      lastRequest.value = requestPayload
      recommendations.value = data.recommendations
      reasoning.value = data.reasoning || ''
      basedOnItems.value = data.basedOnItems
      extractedMenuItems.value = data.extractedMenuItems || []
      savedThreadId.value = null
    } catch (error: unknown) {
      console.error('Failed to get recommendations:', error)
      const apiError = error as { response?: { data?: { error?: string } } }
      reasoning.value = apiError.response?.data?.error || 'Failed to generate recommendations'
      isError.value = true
    } finally {
      isLoadingRecommendations.value = false
    }
  }

  function toggleType(type: string) {
    const index = selectedTypes.value.indexOf(type)
    if (index > -1) {
      selectedTypes.value.splice(index, 1)
    } else {
      selectedTypes.value.push(type)
    }
  }

  function reset() {
    menuPhotoUrl.value = ''
    recommendations.value = []
    reasoning.value = ''
    basedOnItems.value = []
    extractedMenuItems.value = []
    preferences.value = ''
    selectedTypes.value = []
    isError.value = false
    savedItems.value = new Set()
    savingItems.value = new Set()
    isSavingThread.value = false
    savedThreadId.value = null
    lastRequest.value = null
  }

  async function saveToWishlist(rec: RecommendedItem, index: number) {
    if (savedItems.value.has(index) || savingItems.value.has(index)) return

    savingItems.value = new Set(savingItems.value).add(index)
    try {
      await itemsApi.createWishlistItem({
        name: rec.name,
        type: rec.type,
        brand: rec.brand || undefined,
        notes: rec.reason,
      })
      savedItems.value = new Set(savedItems.value).add(index)
    } catch (error) {
      console.error('Failed to save to wishlist:', error)
    } finally {
      const newSet = new Set(savingItems.value)
      newSet.delete(index)
      savingItems.value = newSet
    }
  }

  async function saveThread() {
    if (isSavingThread.value || savedThreadId.value || !lastRequest.value || recommendations.value.length === 0) return

    isSavingThread.value = true
    try {
      const { data } = await recommendationsApi.saveThread(lastRequest.value, {
        recommendations: recommendations.value,
        reasoning: reasoning.value,
        basedOnItems: basedOnItems.value,
        extractedMenuItems: extractedMenuItems.value,
      })
      savedThreadId.value = data.id
    } catch (error) {
      console.error('Failed to save recommendation thread:', error)
    } finally {
      isSavingThread.value = false
    }
  }

  return {
    // State
    profileData,
    isLoadingProfile,
    preferences,
    menuPhotoUrl,
    selectedTypes,
    recommendations,
    reasoning,
    basedOnItems,
    extractedMenuItems,
    isLoadingRecommendations,
    isUploadingPhoto,
    isError,
    savedItems,
    savingItems,
    isSavingThread,
    savedThreadId,
    // Computed
    hasRecommendations,
    // Actions
    loadUserProfile,
    uploadMenuPhoto,
    getRecommendations,
    toggleType,
    reset,
    saveToWishlist,
    saveThread,
  }
})
