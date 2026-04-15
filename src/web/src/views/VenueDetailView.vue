<script setup lang="ts">
import { ref, inject, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { venuesApi, type Venue } from '../services/venues'
import { itemsApi, type Item } from '../services/items'
import { thoughtsApi, type Thought } from '../services/thoughts'
import { useAuthStore } from '../stores/auth'
import { RefreshKey } from '../composables/refreshKey'
import StarRating from '../components/common/StarRating.vue'
import ThoughtsList from '../components/common/ThoughtsList.vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const registerRefresh = inject(RefreshKey)

const venue = ref<Venue | null>(null)
const linkedItems = ref<Item[]>([])
const isEditing = ref(false)
const isDeleting = ref(false)
const isSaving = ref(false)
const friendThoughts = ref<Thought[]>([])
const isLoadingThoughts = ref(false)

const editName = ref('')
const editAddress = ref('')
const editWebsite = ref('')
const editType = ref('other')
const editRating = ref(0)
const pendingPhotoDeletes = ref(new Set<string>())
const pendingPhotoAdds = ref<{ file: File; previewUrl: string }[]>([])
const photoFileInput = ref<HTMLInputElement | null>(null)
const isUploadingPhoto = ref(false)

// Item association
const allItems = ref<Item[]>([])
const selectedItemIds = ref<Set<string>>(new Set())
const itemSearchQuery = ref('')

// Labels
const editLabels = ref<string[]>([])
const newLabelInput = ref('')
const suggestedLabels = [
  'to-try', 'date night', 'craft cocktails', 'outdoor seating', 'live music',
  'happy hour', 'brunch', 'fine dining', 'casual', 'rooftop',
  'speakeasy', 'sports bar', 'wine bar', 'tiki', 'gastropub',
  'late night', 'family friendly', 'dog friendly', 'waterfront',
]

const venueTypeOptions = [
  { label: 'Bar', value: 'bar' },
  { label: 'Lounge', value: 'lounge' },
  { label: 'Restaurant', value: 'restaurant' },
  { label: 'Other', value: 'other' },
]

async function refreshVenue() {
  const id = route.params.id as string
  const { data } = await venuesApi.get(id)
  venue.value = data
  resetEditFields(data)

  try {
    const { data: itemsData } = await venuesApi.getItems(id)
    linkedItems.value = itemsData.items as Item[]
  } catch {
    linkedItems.value = []
  }
}

registerRefresh?.(refreshVenue)

onMounted(async () => {
  await refreshVenue()
  loadThoughts()
})

async function loadThoughts() {
  if (!venue.value || !auth.user) return
  isLoadingThoughts.value = true
  try {
    const res = await thoughtsApi.getForTarget('venue', venue.value.id, auth.user.id)
    friendThoughts.value = res.data
  } catch { /* silent */ }
  isLoadingThoughts.value = false
}

async function handleDeleteThought(thought: Thought) {
  try {
    await thoughtsApi.remove(thought.id)
    friendThoughts.value = friendThoughts.value.filter(t => t.id !== thought.id)
  } catch { /* silent */ }
}

function resetEditFields(data: Venue) {
  editName.value = data.name
  editAddress.value = data.address ?? ''
  editWebsite.value = data.website ?? ''
  editType.value = data.type
  editRating.value = data.rating ?? 0
  editLabels.value = [...(data.labels ?? [])]
  newLabelInput.value = ''
}

function startEditing() {
  if (venue.value) resetEditFields(venue.value)
  pendingPhotoDeletes.value = new Set()
  pendingPhotoAdds.value = []
  selectedItemIds.value = new Set(linkedItems.value.map(i => i.id))
  itemSearchQuery.value = ''
  isEditing.value = true
  loadAllItems()
}

async function loadAllItems() {
  try {
    const { data } = await itemsApi.list(undefined, undefined, 'collected')
    allItems.value = data.items
  } catch {
    allItems.value = []
  }
}

const filteredItems = computed(() => {
  const q = itemSearchQuery.value.toLowerCase().trim()
  const items = allItems.value.filter(i => {
    if (!q) return true
    return i.name.toLowerCase().includes(q) || i.type.toLowerCase().includes(q)
  })
  // Show selected items first, then unselected
  return items.sort((a, b) => {
    const aSelected = selectedItemIds.value.has(a.id) ? 0 : 1
    const bSelected = selectedItemIds.value.has(b.id) ? 0 : 1
    return aSelected - bSelected
  }).slice(0, 20)
})

function toggleItemLink(itemId: string) {
  if (selectedItemIds.value.has(itemId)) {
    selectedItemIds.value.delete(itemId)
  } else {
    selectedItemIds.value.add(itemId)
  }
}

function addLabel(label: string) {
  const normalized = label.trim().toLowerCase()
  if (normalized && !editLabels.value.includes(normalized)) {
    editLabels.value.push(normalized)
  }
  newLabelInput.value = ''
  showLabelDropdown.value = false
}

function removeLabel(label: string) {
  editLabels.value = editLabels.value.filter(l => l !== label)
}

const showLabelDropdown = ref(false)

const filteredSuggestions = computed(() => {
  const q = newLabelInput.value.trim().toLowerCase()
  const available = suggestedLabels.filter(l => !editLabels.value.includes(l))
  if (!q) return available
  return available.filter(l => l.includes(q))
})

const showCustomOption = computed(() => {
  const q = newLabelInput.value.trim().toLowerCase()
  return q && !suggestedLabels.includes(q) && !editLabels.value.includes(q)
})

function onLabelInputFocus() {
  showLabelDropdown.value = true
}

function onLabelInputBlur() {
  // Delay to allow click on dropdown item
  setTimeout(() => { showLabelDropdown.value = false }, 200)
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
  if (!venue.value) throw new Error('No venue')
  const { data } = await venuesApi.getPhotoUploadUrl(venue.value.id, file.name)
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

async function saveEdits() {
  if (!venue.value) return
  isSaving.value = true
  try {
    // Delete marked photos
    for (const url of pendingPhotoDeletes.value) {
      try {
        const { data } = await venuesApi.removePhoto(venue.value.id, url)
        venue.value = data
      } catch { /* continue with other ops */ }
    }

    // Upload and add new photos
    for (const pending of pendingPhotoAdds.value) {
      try {
        const blobUrl = await uploadNewPhoto(pending.file)
        const { data } = await venuesApi.addPhoto(venue.value.id, blobUrl)
        venue.value = data
        URL.revokeObjectURL(pending.previewUrl)
      } catch { /* continue with other ops */ }
    }

    // Save other fields
    const { data } = await venuesApi.update(venue.value.id, {
      name: editName.value.trim(),
      address: editAddress.value.trim() || undefined,
      website: editWebsite.value.trim() || undefined,
      type: editType.value,
      rating: editRating.value || undefined,
      labels: editLabels.value,
    })
    venue.value = data

    // Update item associations
    const currentLinkedIds = new Set(linkedItems.value.map(i => i.id))
    const venueInfo = {
      venueId: venue.value.id,
      name: venue.value.name,
      address: venue.value.address,
    }

    // Link newly selected items
    for (const itemId of selectedItemIds.value) {
      if (!currentLinkedIds.has(itemId)) {
        try {
          await itemsApi.update(itemId, { venue: venueInfo })
        } catch { /* continue */ }
      }
    }

    // Unlink removed items
    for (const itemId of currentLinkedIds) {
      if (!selectedItemIds.value.has(itemId)) {
        try {
          await itemsApi.update(itemId, { venue: { name: '' } })
        } catch { /* continue */ }
      }
    }

    // Refresh linked items
    try {
      const { data: itemsData } = await venuesApi.getItems(venue.value.id)
      linkedItems.value = itemsData.items as Item[]
    } catch {
      linkedItems.value = []
    }

    isEditing.value = false
  } finally {
    isSaving.value = false
  }
}

async function deleteVenue() {
  if (!venue.value) return
  isDeleting.value = true
  try {
    await venuesApi.delete(venue.value.id)
    router.replace('/venues')
  } finally {
    isDeleting.value = false
  }
}

const photoUrls = computed(() => venue.value?.photoUrls ?? [])
</script>

<template>
  <div class="p-4 max-w-lg mx-auto" v-if="venue">
    <!-- Back button -->
    <button @click="router.back()" class="text-[#96BEE6] hover:text-white text-sm mb-4 flex items-center gap-1">
      <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
      </svg>
      Back
    </button>

    <!-- Photos (view mode) -->
    <div v-if="!isEditing && photoUrls.length" class="mb-4 -mx-4">
      <div class="flex gap-2 px-4 overflow-x-auto">
        <img
          v-for="url in photoUrls"
          :key="url"
          :src="url"
          class="w-32 h-32 object-cover rounded-xl shrink-0"
        />
      </div>
    </div>

    <!-- View mode -->
    <template v-if="!isEditing">
      <div class="space-y-4">
        <div>
          <span class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6] capitalize">{{ venue.type }}</span>
          <h2 class="text-2xl font-bold text-white mt-2">{{ venue.name }}</h2>
        </div>

        <div v-if="venue.rating" class="flex items-center gap-2">
          <StarRating :rating="venue.rating" size="md" />
        </div>

        <div v-if="venue.address" class="text-[#96BEE6] text-sm">{{ venue.address }}</div>

        <a v-if="venue.website" :href="venue.website" target="_blank" class="text-[#96BEE6] text-sm hover:text-[#96BEE6] block truncate">
          {{ venue.website }}
        </a>

        <div v-if="venue.labels?.length" class="flex flex-wrap gap-1.5">
          <span
            v-for="label in venue.labels"
            :key="label"
            class="text-xs px-2.5 py-1 rounded-full bg-[#1e407c]/30 text-[#96BEE6] border border-[#1e407c]/50"
          >
            {{ label }}
          </span>
        </div>

        <div class="flex gap-2">
          <button
            @click="startEditing"
            class="flex-1 bg-[#1e407c] hover:bg-[#2a5299] text-white py-2.5 min-h-[44px] rounded-xl text-sm font-medium"
          >
            Edit
          </button>
          <button
            @click="deleteVenue"
            :disabled="isDeleting"
            class="px-4 py-2.5 min-h-[44px] bg-[#0a2a52] text-red-400 hover:bg-[#1e407c] rounded-xl text-sm"
          >
            {{ isDeleting ? 'Deleting...' : 'Delete' }}
          </button>
        </div>
      </div>

      <!-- Linked items -->
      <div v-if="linkedItems.length" class="mt-6">
        <h3 class="text-sm font-medium text-[#96BEE6] mb-3">Linked Items</h3>
        <div class="space-y-2">
          <router-link
            v-for="item in linkedItems"
            :key="item.id"
            :to="`/items/${item.id}`"
            class="block bg-[#041e3e] border border-[#0a2a52] rounded-xl p-3 hover:border-[#1e407c]/50 transition-colors"
          >
            <div class="flex items-center gap-3">
              <img
                v-if="item.photoUrls?.length"
                :src="item.photoUrls[0]"
                class="w-10 h-10 object-cover rounded-lg shrink-0"
              />
              <div class="flex-1 min-w-0">
                <span class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6]">{{ item.type }}</span>
                <h4 class="font-medium text-white truncate text-sm">{{ item.name }}</h4>
              </div>
            </div>
          </router-link>
        </div>
      </div>
    </template>

    <!-- Friend Thoughts (read-only mode only) -->
    <div v-if="!isEditing && friendThoughts.length > 0" class="mt-6">
      <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide mb-3">
        Friend Thoughts ({{ friendThoughts.length }})
      </h3>
      <ThoughtsList :thoughts="friendThoughts" @delete="handleDeleteThought" />
    </div>

    <!-- Edit mode -->
    <template v-else>
      <!-- Photos (edit mode) -->
      <div class="mb-4">
        <label class="block text-sm text-[#96BEE6] mb-2">Photos</label>
        <div class="flex gap-2 overflow-x-auto pb-2">
          <!-- Existing photos with delete toggle -->
          <div
            v-for="(url, i) in photoUrls"
            :key="'existing-' + i"
            class="relative flex-shrink-0"
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
            >&times;</button>
            <button
              v-else
              @click="unmarkPhotoForDelete(url)"
              class="absolute inset-0 flex items-center justify-center text-xs text-white/80 bg-black/40 rounded-xl"
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
              class="h-32 w-32 object-cover rounded-xl border-2 border-[#1e407c]"
            />
            <button
              @click="removePendingPhoto(i)"
              class="absolute top-1 right-1 bg-black/70 text-red-400 hover:text-red-300 rounded-full w-11 h-11 flex items-center justify-center text-xs"
              title="Remove"
            >&times;</button>
            <span class="absolute bottom-1 left-1 text-[10px] bg-[#1e407c]/80 text-white px-1.5 py-0.5 rounded">New</span>
          </div>

          <!-- Add photo button -->
          <button
            @click="triggerPhotoUpload"
            :disabled="isUploadingPhoto"
            class="h-32 w-32 flex-shrink-0 flex flex-col items-center justify-center border-2 border-dashed border-[#1e407c]/50 rounded-xl text-[#96BEE6]/70 hover:text-[#96BEE6] hover:border-[#1e407c] transition-colors"
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

      <div class="space-y-4">
        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">Name</label>
          <input
            v-model="editName"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-[#1e407c]"
          />
        </div>

        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">Type</label>
          <select
            v-model="editType"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-[#1e407c] appearance-none"
          >
            <option v-for="opt in venueTypeOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </div>

        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">Address</label>
          <input
            v-model="editAddress"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-[#1e407c]"
          />
        </div>

        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">Website</label>
          <input
            v-model="editWebsite"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-[#1e407c]"
          />
        </div>

        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">Rating</label>
          <StarRating :rating="editRating" size="lg" interactive @update:rating="editRating = $event" />
        </div>

        <!-- Labels -->
        <div>
          <label class="block text-sm text-[#96BEE6] mb-2">Labels</label>

          <!-- Current labels -->
          <div v-if="editLabels.length" class="flex flex-wrap gap-1.5 mb-2">
            <span
              v-for="label in editLabels"
              :key="label"
              class="inline-flex items-center gap-1 text-xs px-2.5 py-1 rounded-full bg-[#1e407c]/30 text-[#96BEE6] border border-[#1e407c]/50"
            >
              {{ label }}
              <button @click="removeLabel(label)" class="hover:text-red-400 ml-0.5 text-base leading-none">&times;</button>
            </span>
          </div>

          <!-- Typeahead label input -->
          <div class="relative mb-2">
            <input
              v-model="newLabelInput"
              placeholder="Type to search or add labels..."
              class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-2 text-white text-sm placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
              @keydown.enter.prevent="addLabel(newLabelInput)"
              @focus="onLabelInputFocus"
              @blur="onLabelInputBlur"
            />
            <!-- Dropdown -->
            <div
              v-if="showLabelDropdown && (filteredSuggestions.length || showCustomOption)"
              class="absolute left-0 right-0 top-full mt-1 bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl overflow-hidden z-10 max-h-48 overflow-y-auto"
            >
              <button
                v-if="showCustomOption"
                @mousedown.prevent="addLabel(newLabelInput)"
                class="w-full text-left px-3 py-2 text-sm text-white hover:bg-[#1e407c]/30 border-b border-[#1e407c]/30"
              >
                Add "{{ newLabelInput.trim().toLowerCase() }}"
              </button>
              <button
                v-for="label in filteredSuggestions"
                :key="label"
                @mousedown.prevent="addLabel(label)"
                class="w-full text-left px-3 py-2 text-sm text-[#96BEE6] hover:bg-[#1e407c]/30"
              >
                {{ label }}
              </button>
            </div>
          </div>
        </div>

        <!-- Linked Items Picker -->
        <div>
          <label class="block text-sm text-[#96BEE6] mb-2">Linked Collection Items</label>
          <input
            v-model="itemSearchQuery"
            type="text"
            placeholder="Search items..."
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-[#1e407c] mb-2"
          />
          <div class="max-h-48 overflow-y-auto space-y-1 rounded-xl border border-[#0a2a52] bg-[#041e3e]/50 p-2">
            <button
              v-for="itm in filteredItems"
              :key="itm.id"
              @click="toggleItemLink(itm.id)"
              class="w-full flex items-center gap-3 px-3 py-2 rounded-lg text-left transition-colors min-h-[44px]"
              :class="selectedItemIds.has(itm.id) ? 'bg-[#1e407c]/30 border border-[#1e407c]/50' : 'hover:bg-[#0a2a52]'"
            >
              <div
                class="w-5 h-5 rounded border-2 flex items-center justify-center flex-shrink-0"
                :class="selectedItemIds.has(itm.id) ? 'border-[#96BEE6] bg-[#2a5299]' : 'border-[#1e407c]'"
              >
                <svg v-if="selectedItemIds.has(itm.id)" class="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="3">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                </svg>
              </div>
              <img
                v-if="itm.photoUrls?.length"
                :src="itm.photoUrls[0]"
                class="w-8 h-8 object-cover rounded-lg flex-shrink-0"
              />
              <div class="flex-1 min-w-0">
                <p class="text-sm text-white truncate">{{ itm.name }}</p>
                <span class="text-xs text-[#96BEE6]/70 capitalize">{{ itm.type }}</span>
              </div>
            </button>
            <p v-if="!filteredItems.length" class="text-xs text-[#4a7aa5]/60 text-center py-3">No items found</p>
          </div>
          <p class="text-xs text-[#4a7aa5]/60 mt-1">{{ selectedItemIds.size }} item{{ selectedItemIds.size === 1 ? '' : 's' }} selected</p>
        </div>

        <div class="flex gap-2">
          <button
            @click="saveEdits"
            :disabled="isSaving"
            class="flex-1 bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#1e407c] disabled:text-[#96BEE6]/70 text-white py-2.5 min-h-[44px] rounded-xl text-sm font-medium"
          >
            {{ isSaving ? 'Saving...' : 'Save' }}
          </button>
          <button
            @click="isEditing = false"
            class="px-4 py-2.5 min-h-[44px] bg-[#0a2a52] text-[#96BEE6] rounded-xl text-sm hover:bg-[#1e407c]"
          >
            Cancel
          </button>
        </div>
      </div>
    </template>
  </div>
</template>
