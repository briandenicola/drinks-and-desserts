import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { recommendationsApi, type RecommendedItem, type UserRatingProfile } from '../services/recommendations'
import { capturesApi } from '../services/captures'
import { itemsApi } from '../services/items'

export const useRecommendationsStore = defineStore('recommendations', () => {
  const preferences = ref('')
  const isLoadingRecommendations = ref(false)
  const isLoadingProfile = ref(false)
  const isUploadingPhoto = ref(false)
  const isError = ref(false)
  const recommendations = ref<RecommendedItem[]>([])
  const reasoning = ref('')
  const basedOnItems = ref<string[]>([])
  const extractedMenuItems = ref<string[]>([])
  const menuPhotoUrl = ref('')
  const profileData = ref<UserRatingProfile | null>(null)
  const selectedTypes = ref<string[]>([])
  const savedItems = ref<Set<number>>(new Set())
  const savingItems = ref<Set<number>>(new Set())

  const hasRecommendations = computed(() => recommendations.value.length > 0)

  async function loadUserProfile() {
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
    isUploadingPhoto.value = true
    try {
      const { data } = await capturesApi.getUploadUrl(photo.name)

      const headers: Record<string, string> = {
        'Content-Type': photo.type,
      }

      const uploadHost = new URL(data.uploadUrl).hostname
      if (uploadHost.endsWith('.blob.core.windows.net') || uploadHost.includes('devstoreaccount')) {
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

  async function getRecommendations(photo?: File) {
    isLoadingRecommendations.value = true
    isError.value = false
    try {
      let photoUrl = menuPhotoUrl.value

      if (photo) {
        photoUrl = await uploadMenuPhoto(photo)
      }

      const { data } = await recommendationsApi.getRecommendations({
        preferences: preferences.value || undefined,
        menuPhoto: photoUrl || undefined,
        itemTypes: selectedTypes.value.length > 0 ? selectedTypes.value : undefined,
        limit: 5,
      })

      recommendations.value = data.recommendations
      reasoning.value = data.reasoning || ''
      basedOnItems.value = data.basedOnItems
      extractedMenuItems.value = data.extractedMenuItems || []
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
  }

  async function saveToWishlist(rec: RecommendedItem, index: number) {
    if (savedItems.value.has(index) || savingItems.value.has(index)) return

    savingItems.value.add(index)
    try {
      await itemsApi.createWishlistItem({
        name: rec.name,
        type: rec.type,
        brand: rec.brand || undefined,
        notes: rec.reason,
      })
      savedItems.value.add(index)
    } catch (error) {
      console.error('Failed to save to wishlist:', error)
    } finally {
      savingItems.value.delete(index)
    }
  }

  return {
    preferences,
    isLoadingRecommendations,
    isLoadingProfile,
    isUploadingPhoto,
    isError,
    recommendations,
    reasoning,
    basedOnItems,
    extractedMenuItems,
    menuPhotoUrl,
    profileData,
    selectedTypes,
    savedItems,
    savingItems,
    hasRecommendations,
    loadUserProfile,
    getRecommendations,
    toggleType,
    reset,
    saveToWishlist,
  }
})
