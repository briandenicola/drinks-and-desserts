<script setup lang="ts">
import { ref, inject, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useItemsStore } from '../stores/items'
import { itemsApi, type Item } from '../services/items'
import StarRating from '../components/common/StarRating.vue'
import AutocompleteInput from '../components/common/AutocompleteInput.vue'
import { RefreshKey } from '../composables/refreshKey'

const route = useRoute()
const router = useRouter()
const itemsStore = useItemsStore()
const registerRefresh = inject(RefreshKey)

const item = ref<Item | null>(null)
const isEditing = ref(false)
const isSaving = ref(false)
const isAddingNote = ref(false)
const showDeleteConfirm = ref(false)
const editRating = ref(0)
const editName = ref('')
const editType = ref('')
const editBrand = ref('')
const editVenueName = ref('')
const editVenueAddress = ref('')
const editTags = ref<string[]>([])
const newTag = ref('')
const newJournalEntry = ref('')

// Photo management
const isUploadingPhoto = ref(false)
const photoFileInput = ref<HTMLInputElement | null>(null)
const pendingPhotoDeletes = ref<Set<string>>(new Set())
const pendingPhotoAdds = ref<{ file: File; previewUrl: string }[]>([])

// Lightbox
const lightboxUrl = ref<string | null>(null)
const lightboxScale = ref(1)
const lightboxTranslateX = ref(0)
const lightboxTranslateY = ref(0)
const isPanning = ref(false)
let pinchStartDist = 0
let pinchStartScale = 1
let panStartX = 0
let panStartY = 0

function openLightbox(url: string) {
  lightboxUrl.value = url
  lightboxScale.value = 1
  lightboxTranslateX.value = 0
  lightboxTranslateY.value = 0
}

function closeLightbox() {
  lightboxUrl.value = null
}

function onLightboxWheel(e: WheelEvent) {
  e.preventDefault()
  const delta = e.deltaY > 0 ? 0.9 : 1.1
  lightboxScale.value = Math.min(Math.max(lightboxScale.value * delta, 0.5), 5)
}

function onLightboxTouchStart(e: TouchEvent) {
  if (e.touches.length === 2) {
    pinchStartDist = Math.hypot(
      e.touches[0].clientX - e.touches[1].clientX,
      e.touches[0].clientY - e.touches[1].clientY
    )
    pinchStartScale = lightboxScale.value
  } else if (e.touches.length === 1 && lightboxScale.value > 1) {
    isPanning.value = true
    panStartX = e.touches[0].clientX - lightboxTranslateX.value
    panStartY = e.touches[0].clientY - lightboxTranslateY.value
  }
}

function onLightboxTouchMove(e: TouchEvent) {
  if (e.touches.length === 2) {
    e.preventDefault()
    const dist = Math.hypot(
      e.touches[0].clientX - e.touches[1].clientX,
      e.touches[0].clientY - e.touches[1].clientY
    )
    lightboxScale.value = Math.min(Math.max(pinchStartScale * (dist / pinchStartDist), 0.5), 5)
  } else if (e.touches.length === 1 && isPanning.value) {
    e.preventDefault()
    lightboxTranslateX.value = e.touches[0].clientX - panStartX
    lightboxTranslateY.value = e.touches[0].clientY - panStartY
  }
}

function onLightboxTouchEnd() {
  isPanning.value = false
}

function resetLightboxZoom() {
  lightboxScale.value = 1
  lightboxTranslateX.value = 0
  lightboxTranslateY.value = 0
}

// Suggestions for autocomplete
const nameSuggestions = ref<string[]>([])
const brandSuggestions = ref<string[]>([])
const tagSuggestions = ref<string[]>([])

const typeOptions = [
  { label: 'Whiskey', value: 'whiskey' },
  { label: 'Wine', value: 'wine' },
  { label: 'Cocktail', value: 'cocktail' },
  { label: 'Vodka', value: 'vodka' },
  { label: 'Gin', value: 'gin' },
  { label: 'Cigar', value: 'cigar' },
  { label: 'Venue', value: 'venue' },
  { label: 'Custom', value: 'custom' },
]

async function loadSuggestions() {
  try {
    const { data } = await itemsApi.getSuggestions()
    nameSuggestions.value = data.names
    brandSuggestions.value = data.brands
    tagSuggestions.value = data.tags
  } catch { /* ignore */ }
}

async function refreshItem() {
  const { data } = await itemsApi.get(route.params.id as string)
  item.value = data
  resetEditFields(data)
}

registerRefresh?.(refreshItem)

onMounted(async () => {
  await refreshItem()
})

function resetEditFields(data: Item) {
  editRating.value = data.userRating ?? 0
  editName.value = data.name ?? ''
  editType.value = data.type ?? 'custom'
  editBrand.value = data.brand ?? ''
  editVenueName.value = data.venue?.name ?? ''
  editVenueAddress.value = data.venue?.address ?? ''
  editTags.value = [...(data.tags ?? [])]
}

function startEditing() {
  if (item.value) resetEditFields(item.value)
  pendingPhotoDeletes.value = new Set()
  pendingPhotoAdds.value = []
  isEditing.value = true
  loadSuggestions()
}

function markPhotoForDelete(url: string) {
  pendingPhotoDeletes.value.add(url)
}

function unmarkPhotoForDelete(url: string) {
  pendingPhotoDeletes.value.delete(url)
}

function triggerPhotoUpload() {
  photoFileInput.value?.click()
}

function onPhotoFilesSelected(event: Event) {
  const input = event.target as HTMLInputElement
  if (!input.files) return
  for (const file of Array.from(input.files)) {
    if (!file.type.startsWith('image/')) continue
    const previewUrl = URL.createObjectURL(file)
    pendingPhotoAdds.value.push({ file, previewUrl })
  }
  input.value = ''
}

function removePendingPhoto(index: number) {
  const removed = pendingPhotoAdds.value.splice(index, 1)
  if (removed[0]) URL.revokeObjectURL(removed[0].previewUrl)
}

async function uploadNewPhoto(file: File): Promise<string> {
  if (!item.value) throw new Error('No item')
  const { data } = await itemsApi.getPhotoUploadUrl(item.value.id, file.name)

  const headers: Record<string, string> = { 'Content-Type': file.type }
  if (data.uploadUrl.includes('blob.core.windows.net') || data.uploadUrl.includes('devstoreaccount')) {
    headers['x-ms-blob-type'] = 'BlockBlob'
  } else {
    const token = localStorage.getItem('whiskey_and_smokes_token')
    if (token) headers['Authorization'] = `Bearer ${token}`
  }

  await fetch(data.uploadUrl, { method: 'PUT', headers, body: file })
  return data.blobUrl
}

function addTag() {
  const tag = newTag.value.trim().toLowerCase()
  if (tag && !editTags.value.includes(tag)) {
    editTags.value.push(tag)
  }
  newTag.value = ''
}

function removeTag(tag: string) {
  editTags.value = editTags.value.filter(t => t !== tag)
}

async function save() {
  if (!item.value) return
  isSaving.value = true
  try {
    // Delete marked photos
    for (const url of pendingPhotoDeletes.value) {
      try {
        const { data } = await itemsApi.removePhoto(item.value.id, url)
        item.value = data
      } catch { /* continue with other ops */ }
    }

    // Upload and add new photos
    for (const pending of pendingPhotoAdds.value) {
      try {
        const blobUrl = await uploadNewPhoto(pending.file)
        const { data } = await itemsApi.addPhoto(item.value.id, blobUrl)
        item.value = data
        URL.revokeObjectURL(pending.previewUrl)
      } catch { /* continue with other ops */ }
    }

    // Save other fields
    const updated = await itemsStore.updateItem(item.value.id, {
      name: editName.value || undefined,
      type: editType.value || undefined,
      brand: editBrand.value || undefined,
      venue: editVenueName.value ? { name: editVenueName.value, address: editVenueAddress.value || undefined } : undefined,
      userRating: editRating.value || undefined,
      tags: editTags.value,
      status: 'reviewed',
    })
    item.value = updated
    pendingPhotoDeletes.value = new Set()
    pendingPhotoAdds.value = []
    isEditing.value = false
  } finally {
    isSaving.value = false
  }
}

async function addJournalEntry() {
  if (!item.value || !newJournalEntry.value.trim()) return
  isAddingNote.value = true
  try {
    const updated = await itemsStore.updateItem(item.value.id, {
      journalEntry: newJournalEntry.value.trim(),
    })
    item.value = updated
    newJournalEntry.value = ''
  } finally {
    isAddingNote.value = false
  }
}

function formatDate(dateStr: string): string {
  const d = new Date(dateStr)
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
    + ' at '
    + d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })
}

async function deleteItem() {
  if (!item.value) return
  await itemsStore.deleteItem(item.value.id)
  router.push('/items')
}

function isAiGenerated(data: Item): boolean {
  return (data.aiConfidence != null && data.aiConfidence > 0)
}
</script>

<template>
  <div v-if="!item" class="p-4 text-stone-500 text-center py-12">Loading...</div>

  <div v-else class="p-4 max-w-lg mx-auto">
    <!-- Top bar: Back + actions -->
    <div class="flex items-center justify-between mb-4">
      <button @click="router.back()" class="text-stone-400 hover:text-stone-200 hover:opacity-80 text-sm min-h-[44px] min-w-[44px] px-2 py-2 transition-opacity">
        ← Back
      </button>
      <div class="flex items-center gap-3">
        <!-- View mode: Edit + Delete icons -->
        <template v-if="!isEditing">
          <button @click="startEditing" class="text-amber-500 hover:text-amber-400 p-2.5 min-h-[44px] min-w-[44px]" title="Edit">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
          </button>
          <button @click="showDeleteConfirm = true" class="text-red-400 hover:text-red-300 p-2.5 min-h-[44px] min-w-[44px]" title="Delete">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
        </template>
        <!-- Edit mode: Save + Cancel icons -->
        <template v-else>
          <button @click="save" :disabled="isSaving" class="text-green-400 hover:text-green-300 disabled:text-stone-600 p-2.5 min-h-[44px] min-w-[44px]" title="Save">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
            </svg>
          </button>
          <button @click="isEditing = false" class="text-stone-400 hover:text-stone-200 p-2.5 min-h-[44px] min-w-[44px]" title="Cancel">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </template>
      </div>
    </div>

    <!-- Photos (view mode) -->
    <div v-if="!isEditing && item.photoUrls.length" class="mb-4 -mx-4">
      <div class="flex gap-2 overflow-x-auto px-4 pb-2">
        <img
          v-for="(url, i) in item.photoUrls"
          :key="i"
          :src="url"
          class="h-48 object-cover rounded-xl cursor-pointer hover:ring-2 hover:ring-amber-600 transition-all"
          @click="openLightbox(url)"
        />
      </div>
    </div>

    <!-- Photos (edit mode) -->
    <div v-if="isEditing" class="mb-4">
      <label class="block text-sm text-stone-400 mb-2">Photos</label>
      <div class="flex gap-2 overflow-x-auto pb-2">
        <!-- Existing photos with delete toggle -->
        <div
          v-for="(url, i) in item.photoUrls"
          :key="'existing-' + i"
          class="relative flex-shrink-0 group"
        >
          <img
            :src="url"
            class="h-32 w-32 object-cover rounded-xl transition-opacity"
            :class="{ 'opacity-30': pendingPhotoDeletes.has(url) }"
          />
          <button
            v-if="!pendingPhotoDeletes.has(url)"
            @click="markPhotoForDelete(url)"
            class="absolute top-1 right-1 bg-black/70 text-red-400 hover:text-red-300 rounded-full w-11 h-11 flex items-center justify-center text-xs"
            title="Remove photo"
          >×</button>
          <button
            v-else
            @click="unmarkPhotoForDelete(url)"
            class="absolute inset-0 flex items-center justify-center text-xs text-stone-300 bg-black/40 rounded-xl"
          >Undo</button>
        </div>

        <!-- Pending new photos -->
        <div
          v-for="(pending, i) in pendingPhotoAdds"
          :key="'pending-' + i"
          class="relative flex-shrink-0"
        >
          <img
            :src="pending.previewUrl"
            class="h-32 w-32 object-cover rounded-xl border-2 border-amber-600"
          />
          <button
            @click="removePendingPhoto(i)"
            class="absolute top-1 right-1 bg-black/70 text-red-400 hover:text-red-300 rounded-full w-11 h-11 flex items-center justify-center text-xs"
            title="Remove"
          >×</button>
          <span class="absolute bottom-1 left-1 text-[10px] bg-amber-700/80 text-white px-1.5 py-0.5 rounded">New</span>
        </div>

        <!-- Add photo button -->
        <button
          @click="triggerPhotoUpload"
          :disabled="isUploadingPhoto"
          class="h-32 w-32 flex-shrink-0 flex flex-col items-center justify-center border-2 border-dashed border-stone-700 rounded-xl text-stone-500 hover:text-amber-500 hover:border-amber-700 transition-colors"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="w-8 h-8 mb-1" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
          </svg>
          <span class="text-xs">Add</span>
        </button>
      </div>
      <input
        ref="photoFileInput"
        type="file"
        accept="image/*"
        multiple
        class="hidden"
        @change="onPhotoFilesSelected"
      />
    </div>

    <!-- Header -->
    <div class="mb-4">
      <span class="text-xs px-2 py-0.5 rounded-full bg-stone-800 text-stone-400">{{ item.type }}</span>
      <span v-if="item.status === 'ai-draft'" class="text-xs text-amber-500 ml-2">AI Draft — tap edit to review</span>
      <h2 class="text-2xl font-bold mt-2">{{ item.name }}</h2>
      <p v-if="item.brand" class="text-stone-400">{{ item.brand }}</p>
    </div>

    <!-- AI Summary -->
    <div v-if="item.aiSummary" class="bg-stone-900 border border-stone-800 rounded-xl p-4 mb-4">
      <p v-if="isAiGenerated(item)" class="text-xs text-amber-500 mb-1">AI Agent Analysis</p>
      <p v-else class="text-xs text-stone-500 mb-1">Local Extraction (AI agents were not available)</p>
      <p class="text-sm text-stone-300">{{ item.aiSummary }}</p>
      <p v-if="item.aiConfidence" class="text-xs text-stone-600 mt-2">
        Confidence: {{ Math.round(item.aiConfidence * 100) }}%
      </p>
    </div>

    <!-- Details Bar (venue, rating, history) -->
    <div class="bg-stone-900 border border-stone-800 rounded-xl p-4 mb-4 space-y-3">
      <!-- Rating -->
      <div v-if="!isEditing && item.userRating" class="flex items-center gap-2">
        <span class="text-xs text-stone-500 uppercase tracking-wide w-16">Rating</span>
        <StarRating :rating="item.userRating" size="md" />
        <span class="text-xs text-stone-500">{{ item.userRating }}</span>
      </div>

      <!-- Venue -->
      <div v-if="item.venue" class="flex items-start gap-2">
        <span class="text-xs text-stone-500 uppercase tracking-wide w-16 pt-0.5">Location</span>
        <div>
          <p class="text-sm text-stone-300">{{ item.venue.name }}</p>
          <p v-if="item.venue.address" class="text-xs text-stone-500">{{ item.venue.address }}</p>
        </div>
      </div>

      <!-- History Link -->
      <div v-if="item.captureId" class="flex items-center gap-2">
        <span class="text-xs text-stone-500 uppercase tracking-wide w-16">Source</span>
        <router-link
          :to="`/history/${item.captureId}`"
          class="text-sm text-stone-400 hover:text-amber-500 transition-colors"
        >
          View processing history →
        </router-link>
      </div>
    </div>

    <!-- Journal -->
    <div v-if="!isEditing" class="mt-6">
      <h3 class="text-sm font-medium text-stone-400 mb-3">Journal</h3>

      <!-- Existing entries -->
      <div v-if="item.journal?.length" class="space-y-3 mb-4">
        <div
          v-for="(entry, i) in item.journal"
          :key="i"
          class="border-l-2 border-stone-700 pl-3"
        >
          <p class="text-sm text-stone-300">{{ entry.text }}</p>
          <p class="text-xs text-stone-600 mt-1">
            {{ formatDate(entry.date) }}
            <span v-if="entry.source === 'capture'" class="text-stone-700 ml-1">· Quick note</span>
          </p>
        </div>
      </div>
      <p v-else class="text-xs text-stone-600 mb-4">No journal entries yet.</p>

      <!-- Add new entry -->
      <div class="flex gap-2">
        <textarea
          v-model="newJournalEntry"
          rows="2"
          placeholder="Add a thought..."
          class="flex-1 bg-stone-800 border border-stone-700 rounded-xl px-4 py-2 text-sm text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700 resize-none"
          @keydown.meta.enter.prevent="addJournalEntry"
          @keydown.ctrl.enter.prevent="addJournalEntry"
        />
        <button
          @click="addJournalEntry"
          :disabled="!newJournalEntry.trim() || isAddingNote"
          class="self-end px-4 py-2 bg-stone-800 hover:bg-stone-700 disabled:opacity-30 text-amber-500 rounded-xl text-sm font-medium"
        >{{ isAddingNote ? '...' : 'Add' }}</button>
      </div>
    </div>

    <!-- Edit mode -->
    <div v-else class="space-y-4">
      <!-- Name -->
      <div>
        <label class="block text-sm text-stone-400 mb-1">Name</label>
        <AutocompleteInput
          v-model="editName"
          :suggestions="nameSuggestions"
          placeholder="Item name"
        />
      </div>

      <!-- Type -->
      <div>
        <label class="block text-sm text-stone-400 mb-2">Type</label>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="opt in typeOptions"
            :key="opt.value"
            @click="editType = opt.value"
            class="px-3 py-1.5 rounded-full text-xs border transition-colors"
            :class="editType === opt.value
              ? 'bg-amber-700 border-amber-600 text-white'
              : 'bg-stone-800 border-stone-700 text-stone-400'"
          >
            {{ opt.label }}
          </button>
        </div>
      </div>

      <!-- Brand -->
      <div>
        <label class="block text-sm text-stone-400 mb-1">Brand</label>
        <AutocompleteInput
          v-model="editBrand"
          :suggestions="brandSuggestions"
          placeholder="e.g. Four Roses"
        />
      </div>

      <!-- Location -->
      <div>
        <label class="block text-sm text-stone-400 mb-1">Location</label>
        <input
          v-model="editVenueName"
          type="text"
          placeholder="e.g. Rare Books Bar"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700"
        />
        <input
          v-model="editVenueAddress"
          type="text"
          placeholder="Address (optional)"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700 mt-2"
        />
      </div>

      <!-- Star rating -->
      <div>
        <label class="block text-sm text-stone-400 mb-2">Rating</label>
        <div class="flex items-center gap-3">
          <StarRating :rating="editRating" size="lg" interactive @update="editRating = $event" />
          <span class="text-sm text-stone-500">{{ editRating > 0 ? editRating : '' }}</span>
        </div>
      </div>

      <!-- Tags -->
      <div>
        <label class="block text-sm text-stone-400 mb-2">Tags</label>
        <div class="flex flex-wrap gap-2 mb-2">
          <span
            v-for="tag in editTags"
            :key="tag"
            class="inline-flex items-center gap-1 text-sm bg-stone-800 text-stone-300 px-3 py-1 rounded-full"
          >
            {{ tag }}
            <button @click="removeTag(tag)" class="text-stone-500 hover:text-red-400 ml-1">×</button>
          </span>
        </div>
        <div class="flex gap-2">
          <AutocompleteInput
            v-model="newTag"
            :suggestions="tagSuggestions.filter(t => !editTags.includes(t))"
            placeholder="Add a tag..."
            input-class="flex-1 bg-stone-800 border border-stone-700 rounded-xl px-4 py-2 text-sm text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700"
            @keydown.enter.prevent="addTag"
          />
          <button
            @click="addTag"
            :disabled="!newTag.trim()"
            class="px-4 bg-stone-800 hover:bg-stone-700 disabled:opacity-30 text-amber-500 rounded-xl text-sm font-medium"
          >+ Add</button>
        </div>
      </div>
    </div>

    <!-- Tags (read-only, outside edit mode) -->
    <div v-if="!isEditing && item.tags.length" class="mt-4 flex gap-2 flex-wrap">
      <span v-for="tag in item.tags" :key="tag" class="text-xs bg-stone-800 text-stone-400 px-2 py-1 rounded-full">
        {{ tag }}
      </span>
    </div>

    <!-- Delete Confirmation Modal -->
    <Teleport to="body">
      <div v-if="showDeleteConfirm" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="showDeleteConfirm = false">
        <div class="bg-stone-900 border border-stone-700 rounded-2xl p-6 w-full max-w-sm">
          <h3 class="text-lg font-semibold text-red-400 mb-4">Delete Item</h3>
          <p class="text-sm text-stone-300 mb-4">
            Are you sure you want to delete <strong>{{ item.name }}</strong>? This action cannot be undone.
          </p>
          <div class="flex gap-2 justify-end">
            <button @click="showDeleteConfirm = false" class="px-4 py-2 text-sm rounded-xl bg-stone-800 text-stone-400 hover:bg-stone-700">Cancel</button>
            <button @click="deleteItem" class="px-4 py-2 text-sm rounded-xl bg-red-700 text-white hover:bg-red-600">Delete</button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Photo Lightbox Modal -->
    <Teleport to="body">
      <div
        v-if="lightboxUrl"
        class="fixed inset-0 bg-black/80 flex items-center justify-center z-50"
        @click.self="closeLightbox"
        @wheel.prevent="onLightboxWheel"
      >
        <!-- Close button -->
        <button
          @click="closeLightbox"
          class="absolute top-4 right-4 text-white/70 hover:text-white bg-black/40 rounded-full w-10 h-10 flex items-center justify-center text-xl z-10"
        >×</button>

        <!-- Zoom controls -->
        <div class="absolute bottom-6 left-1/2 -translate-x-1/2 flex items-center gap-3 bg-black/50 rounded-full px-4 py-2 z-10">
          <button
            @click="lightboxScale = Math.max(lightboxScale - 0.25, 0.5)"
            class="text-white/70 hover:text-white text-lg w-8 h-8 flex items-center justify-center"
          >-</button>
          <button
            @click="resetLightboxZoom"
            class="text-xs text-white/70 hover:text-white px-2"
          >{{ Math.round(lightboxScale * 100) }}%</button>
          <button
            @click="lightboxScale = Math.min(lightboxScale + 0.25, 5)"
            class="text-white/70 hover:text-white text-lg w-8 h-8 flex items-center justify-center"
          >+</button>
        </div>

        <!-- Navigation arrows (absolute, overlaid on edges) -->
        <button
          v-if="item.photoUrls.length > 1"
          @click.stop="openLightbox(item.photoUrls[(item.photoUrls.indexOf(lightboxUrl!) - 1 + item.photoUrls.length) % item.photoUrls.length])"
          class="absolute left-2 top-1/2 -translate-y-1/2 text-white/50 hover:text-white bg-black/30 rounded-full w-10 h-10 flex items-center justify-center text-2xl z-10"
        >&lsaquo;</button>

        <button
          v-if="item.photoUrls.length > 1"
          @click.stop="openLightbox(item.photoUrls[(item.photoUrls.indexOf(lightboxUrl!) + 1) % item.photoUrls.length])"
          class="absolute right-2 top-1/2 -translate-y-1/2 text-white/50 hover:text-white bg-black/30 rounded-full w-10 h-10 flex items-center justify-center text-2xl z-10"
        >&rsaquo;</button>

        <!-- Centered image -->
        <img
          :src="lightboxUrl"
          class="max-w-[90vw] max-h-[80vh] rounded-2xl object-contain select-none"
          :style="{
            transform: `scale(${lightboxScale}) translate(${lightboxTranslateX / lightboxScale}px, ${lightboxTranslateY / lightboxScale}px)`,
            transition: isPanning ? 'none' : 'transform 0.15s ease'
          }"
          @touchstart="onLightboxTouchStart"
          @touchmove="onLightboxTouchMove"
          @touchend="onLightboxTouchEnd"
          @click.stop
          draggable="false"
        />

        <!-- Photo counter -->
        <div v-if="item.photoUrls.length > 1" class="absolute top-4 left-1/2 -translate-x-1/2 text-xs text-white/50">
          {{ item.photoUrls.indexOf(lightboxUrl!) + 1 }} / {{ item.photoUrls.length }}
        </div>
      </div>
    </Teleport>
  </div>
</template>
