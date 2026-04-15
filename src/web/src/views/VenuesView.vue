<script setup lang="ts">
import { ref, inject, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useVenuesStore } from '../stores/venues'
import { RefreshKey } from '../composables/refreshKey'
import StarRating from '../components/common/StarRating.vue'

const router = useRouter()
const venuesStore = useVenuesStore()
const registerRefresh = inject(RefreshKey)

const showAddForm = ref(false)
const addMode = ref<'manual' | 'url'>('manual')
const newName = ref('')
const newType = ref('restaurant')
const newAddress = ref('')
const newWebsite = ref('')
const newUrl = ref('')
const isAdding = ref(false)
const urlError = ref('')

const venueTypeOptions = [
  { label: 'Bar', value: 'bar' },
  { label: 'Lounge', value: 'lounge' },
  { label: 'Restaurant', value: 'restaurant' },
  { label: 'Other', value: 'other' },
]

registerRefresh?.(async () => {
  await venuesStore.loadVenues(undefined, true)
})

onMounted(() => {
  venuesStore.loadVenues(undefined, true)
})

const sortedVenues = computed(() => {
  return [...venuesStore.venues].sort((a, b) => {
    const ra = a.rating ?? 0
    const rb = b.rating ?? 0
    if (rb !== ra) return rb - ra
    return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
  })
})

async function addVenue() {
  if (!newName.value.trim()) return
  isAdding.value = true
  try {
    const venue = await venuesStore.createVenue({
      name: newName.value.trim(),
      type: newType.value,
      address: newAddress.value.trim() || undefined,
      website: newWebsite.value.trim() || undefined,
    })
    resetForm()
    router.push(`/venues/${venue.id}`)
  } finally {
    isAdding.value = false
  }
}

async function addVenueFromUrl() {
  const url = newUrl.value.trim()
  if (!url) return
  urlError.value = ''

  try {
    new URL(url)
  } catch {
    urlError.value = 'Please enter a valid URL'
    return
  }

  isAdding.value = true
  try {
    const venue = await venuesStore.createVenueFromUrl(url)
    resetForm()
    router.push(`/venues/${venue.id}`)
  } catch {
    urlError.value = 'Failed to process URL. Please try again or add manually.'
  } finally {
    isAdding.value = false
  }
}

function resetForm() {
  newName.value = ''
  newAddress.value = ''
  newWebsite.value = ''
  newUrl.value = ''
  urlError.value = ''
  showAddForm.value = false
  addMode.value = 'manual'
}
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <div class="flex items-center justify-between mb-4">
      <h2 class="text-xl font-bold text-white">Venues</h2>
      <button
        @click="showAddForm = !showAddForm"
        class="text-[#96BEE6] hover:text-white transition-colors"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
        </svg>
      </button>
    </div>

    <!-- Add form -->
    <div v-if="showAddForm" class="mb-4">
      <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
        <!-- Mode toggle -->
        <div class="flex rounded-lg bg-[#0a2a52] p-0.5">
          <button
            @click="addMode = 'manual'"
            :class="[
              'flex-1 py-1.5 text-xs font-medium rounded-md transition-colors',
              addMode === 'manual' ? 'bg-[#1e407c] text-white' : 'text-[#96BEE6] hover:text-white/80'
            ]"
          >
            Manual
          </button>
          <button
            @click="addMode = 'url'"
            :class="[
              'flex-1 py-1.5 text-xs font-medium rounded-md transition-colors',
              addMode === 'url' ? 'bg-[#1e407c] text-white' : 'text-[#96BEE6] hover:text-white/80'
            ]"
          >
            From URL
          </button>
        </div>

        <!-- Manual form -->
        <template v-if="addMode === 'manual'">
          <input
            v-model="newName"
            placeholder="Name (required)"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
          />

          <select
            v-model="newType"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-[#1e407c] appearance-none"
          >
            <option v-for="opt in venueTypeOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>

          <input
            v-model="newAddress"
            placeholder="Address (optional)"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
          />

          <input
            v-model="newWebsite"
            placeholder="Website (optional)"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
          />

          <div class="flex gap-2">
            <button
              @click="addVenue"
              :disabled="isAdding || !newName.trim()"
              class="flex-1 bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#1e407c] disabled:text-[#96BEE6]/70 text-white py-2.5 rounded-xl text-sm font-medium"
            >
              {{ isAdding ? 'Adding...' : 'Add' }}
            </button>
            <button
              @click="resetForm()"
              class="px-4 py-2.5 bg-[#0a2a52] text-[#96BEE6] rounded-xl text-sm hover:bg-[#1e407c]"
            >
              Cancel
            </button>
          </div>
        </template>

        <!-- URL form -->
        <template v-else>
          <p class="text-xs text-[#96BEE6]/70">Paste an Apple Maps, Google Maps, or venue website URL</p>
          <input
            v-model="newUrl"
            placeholder="https://maps.apple.com/..."
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
          />
          <p v-if="urlError" class="text-xs text-red-400">{{ urlError }}</p>

          <div class="flex gap-2">
            <button
              @click="addVenueFromUrl"
              :disabled="isAdding || !newUrl.trim()"
              class="flex-1 bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#1e407c] disabled:text-[#96BEE6]/70 text-white py-2.5 rounded-xl text-sm font-medium"
            >
              {{ isAdding ? 'Extracting...' : 'Extract Venue' }}
            </button>
            <button
              @click="resetForm()"
              class="px-4 py-2.5 bg-[#0a2a52] text-[#96BEE6] rounded-xl text-sm hover:bg-[#1e407c]"
            >
              Cancel
            </button>
          </div>
        </template>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="venuesStore.isLoading && !sortedVenues.length" class="text-[#96BEE6]/70 text-center py-12">
      Loading...
    </div>

    <!-- Empty -->
    <div v-else-if="!sortedVenues.length" class="text-[#96BEE6]/70 text-center py-12">
      <p>No venues yet. Add your favorite spots.</p>
    </div>

    <!-- Venue list -->
    <div v-else class="space-y-3">
      <router-link
        v-for="venue in sortedVenues"
        :key="venue.id"
        :to="`/venues/${venue.id}`"
        class="block bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 hover:border-[#1e407c]/50 transition-colors"
      >
        <div class="flex items-start gap-3">
          <img
            v-if="venue.photoUrls.length"
            :src="venue.photoUrls[0]"
            class="w-16 h-16 object-cover rounded-lg shrink-0"
          />
          <div v-else class="w-16 h-16 bg-[#0a2a52] rounded-lg shrink-0 flex items-center justify-center">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6 text-[#4a7aa5]/60" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
              <path stroke-linecap="round" stroke-linejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>

          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2 mb-1">
              <span class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6] capitalize">{{ venue.type }}</span>
            </div>
            <h3 class="font-medium text-white truncate">{{ venue.name }}</h3>
            <p v-if="venue.address" class="text-sm text-[#96BEE6]/70 truncate">{{ venue.address }}</p>
            <div v-if="venue.rating" class="mt-1">
              <StarRating :rating="venue.rating" size="sm" />
            </div>
            <div v-if="venue.labels?.length" class="flex flex-wrap gap-1 mt-1.5">
              <span
                v-for="label in venue.labels.slice(0, 3)"
                :key="label"
                class="text-[10px] px-1.5 py-0.5 rounded-full bg-[#1e407c]/30 text-[#96BEE6]/80"
              >
                {{ label }}
              </span>
              <span v-if="venue.labels.length > 3" class="text-[10px] text-[#4a7aa5]/60">
                +{{ venue.labels.length - 3 }}
              </span>
            </div>
          </div>
        </div>
      </router-link>
    </div>
  </div>
</template>
