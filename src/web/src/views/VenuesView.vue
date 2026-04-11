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
const newName = ref('')
const newType = ref('restaurant')
const newAddress = ref('')
const newWebsite = ref('')
const isAdding = ref(false)

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
    newName.value = ''
    newAddress.value = ''
    newWebsite.value = ''
    showAddForm.value = false
    router.push(`/venues/${venue.id}`)
  } finally {
    isAdding.value = false
  }
}
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <h2 class="text-xl font-bold text-stone-100 mb-4">Venues</h2>

    <!-- Add button + form -->
    <div class="mb-4">
      <button
        v-if="!showAddForm"
        @click="showAddForm = true"
        class="w-full bg-stone-900 border border-dashed border-stone-700 rounded-xl p-3 text-stone-400 hover:border-stone-500 hover:text-stone-300 transition-colors text-sm"
      >
        + Add venue
      </button>

      <div v-else class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-3">
        <input
          v-model="newName"
          placeholder="Name (required)"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-2.5 text-stone-100 text-sm placeholder-stone-600 focus:outline-none focus:border-amber-700"
        />

        <select
          v-model="newType"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-2.5 text-stone-100 text-sm focus:outline-none focus:border-amber-700 appearance-none"
        >
          <option v-for="opt in venueTypeOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </option>
        </select>

        <input
          v-model="newAddress"
          placeholder="Address (optional)"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-2.5 text-stone-100 text-sm placeholder-stone-600 focus:outline-none focus:border-amber-700"
        />

        <input
          v-model="newWebsite"
          placeholder="Website (optional)"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-2.5 text-stone-100 text-sm placeholder-stone-600 focus:outline-none focus:border-amber-700"
        />

        <div class="flex gap-2">
          <button
            @click="addVenue"
            :disabled="isAdding || !newName.trim()"
            class="flex-1 bg-amber-700 hover:bg-amber-600 disabled:bg-stone-700 disabled:text-stone-500 text-white py-2.5 rounded-xl text-sm font-medium"
          >
            {{ isAdding ? 'Adding...' : 'Add' }}
          </button>
          <button
            @click="showAddForm = false"
            class="px-4 py-2.5 bg-stone-800 text-stone-400 rounded-xl text-sm hover:bg-stone-700"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="venuesStore.isLoading && !sortedVenues.length" class="text-stone-500 text-center py-12">
      Loading...
    </div>

    <!-- Empty -->
    <div v-else-if="!sortedVenues.length" class="text-stone-500 text-center py-12">
      <p>No venues yet. Add your favorite spots.</p>
    </div>

    <!-- Venue list -->
    <div v-else class="space-y-3">
      <router-link
        v-for="venue in sortedVenues"
        :key="venue.id"
        :to="`/venues/${venue.id}`"
        class="block bg-stone-900 border border-stone-800 rounded-xl p-4 hover:border-stone-700 transition-colors"
      >
        <div class="flex items-start gap-3">
          <img
            v-if="venue.photoUrls.length"
            :src="venue.photoUrls[0]"
            class="w-16 h-16 object-cover rounded-lg shrink-0"
          />
          <div v-else class="w-16 h-16 bg-stone-800 rounded-lg shrink-0 flex items-center justify-center">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6 text-stone-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
              <path stroke-linecap="round" stroke-linejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>

          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2 mb-1">
              <span class="text-xs px-2 py-0.5 rounded-full bg-stone-800 text-stone-400 capitalize">{{ venue.type }}</span>
            </div>
            <h3 class="font-medium text-stone-100 truncate">{{ venue.name }}</h3>
            <p v-if="venue.address" class="text-sm text-stone-500 truncate">{{ venue.address }}</p>
            <div v-if="venue.rating" class="mt-1">
              <StarRating :rating="venue.rating" size="sm" />
            </div>
          </div>
        </div>
      </router-link>
    </div>
  </div>
</template>
