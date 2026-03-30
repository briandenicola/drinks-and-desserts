<script setup lang="ts">
import { ref } from 'vue'
import { useCamera } from '../composables/useCamera'
import { useLocation } from '../composables/useLocation'
import { useCapturesStore } from '../stores/captures'
import { capturesApi } from '../services/captures'

const { photos, previews, addFromInput, removePhoto, clearPhotos } = useCamera()
const { location, isLocating, requestLocation } = useLocation()
const capturesStore = useCapturesStore()

const userNote = ref('')
const isSubmitting = ref(false)
const showSuccess = ref(false)

// Auto-request location on mount
requestLocation()

async function uploadPhotos(): Promise<string[]> {
  const urls: string[] = []

  for (const photo of photos.value) {
    const { data } = await capturesApi.getUploadUrl(photo.name)

    const headers: Record<string, string> = {
      'Content-Type': photo.type,
    }

    // Add Azure blob header for SAS uploads, auth header for local dev uploads
    if (data.uploadUrl.includes('blob.core.windows.net') || data.uploadUrl.includes('devstoreaccount')) {
      headers['x-ms-blob-type'] = 'BlockBlob'
    } else {
      // Local dev upload — needs JWT auth
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

    urls.push(data.blobUrl)
  }

  return urls
}

async function submit() {
  if (photos.value.length === 0 && !userNote.value.trim()) return

  isSubmitting.value = true
  try {
    const photoUrls = await uploadPhotos()

    await capturesStore.createCapture({
      photos: photoUrls,
      userNote: userNote.value || undefined,
      location: location.value || undefined,
    })

    // Reset form
    clearPhotos()
    userNote.value = ''
    showSuccess.value = true
    setTimeout(() => { showSuccess.value = false }, 3000)
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <h2 class="text-xl font-semibold mb-4">Quick Capture</h2>

    <!-- Success toast -->
    <div v-if="showSuccess" class="bg-green-900/50 border border-green-700 text-green-300 px-4 py-3 rounded-lg mb-4 text-sm">
      ✅ Captured! AI is processing your entry.
    </div>

    <!-- Photo Capture -->
    <div class="mb-6">
      <label class="block text-sm text-stone-400 mb-2">Photos</label>

      <!-- Photo previews -->
      <div v-if="previews.length" class="flex gap-2 mb-3 overflow-x-auto pb-2">
        <div v-for="(preview, index) in previews" :key="index" class="relative shrink-0">
          <img :src="preview" class="w-20 h-20 object-cover rounded-lg" />
          <button
            @click="removePhoto(index)"
            class="absolute -top-1 -right-1 bg-red-600 text-white rounded-full w-5 h-5 flex items-center justify-center text-xs"
          >×</button>
        </div>
      </div>

      <!-- Camera / Gallery buttons -->
      <div class="flex gap-3">
        <label class="flex-1 bg-stone-800 hover:bg-stone-700 border border-stone-700 rounded-xl py-4 flex flex-col items-center cursor-pointer transition-colors">
          <span class="text-2xl mb-1">📷</span>
          <span class="text-xs text-stone-400">Camera</span>
          <input type="file" accept="image/*" capture="environment" multiple @change="addFromInput" class="hidden" />
        </label>

        <label class="flex-1 bg-stone-800 hover:bg-stone-700 border border-stone-700 rounded-xl py-4 flex flex-col items-center cursor-pointer transition-colors">
          <span class="text-2xl mb-1">🖼️</span>
          <span class="text-xs text-stone-400">Gallery</span>
          <input type="file" accept="image/*" multiple @change="addFromInput" class="hidden" />
        </label>
      </div>
    </div>

    <!-- Quick Note -->
    <div class="mb-6">
      <label class="block text-sm text-stone-400 mb-2">Quick Note <span class="text-stone-600">(optional)</span></label>
      <textarea
        v-model="userNote"
        placeholder="Amazing old fashioned here, also tried a Lagavulin 16..."
        rows="3"
        class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700 resize-none"
      />
    </div>

    <!-- Location indicator -->
    <div class="mb-6 flex items-center gap-2 text-sm">
      <span v-if="isLocating" class="text-stone-500">📍 Getting location...</span>
      <span v-else-if="location" class="text-green-500">📍 Location captured</span>
      <span v-else class="text-stone-600">📍 Location unavailable</span>
    </div>

    <!-- Submit -->
    <button
      @click="submit"
      :disabled="isSubmitting || (photos.length === 0 && !userNote.trim())"
      class="w-full bg-amber-700 hover:bg-amber-600 disabled:bg-stone-700 disabled:text-stone-500 text-white font-semibold py-4 rounded-xl transition-colors text-lg"
    >
      {{ isSubmitting ? 'Uploading...' : 'Capture' }}
    </button>
  </div>
</template>
