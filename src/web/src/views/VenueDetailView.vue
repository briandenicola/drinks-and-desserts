<script setup lang="ts">
import { ref, inject, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { venuesApi, type Venue } from '../services/venues'
import { type Item } from '../services/items'
import { RefreshKey } from '../composables/refreshKey'
import StarRating from '../components/common/StarRating.vue'

const route = useRoute()
const router = useRouter()
const registerRefresh = inject(RefreshKey)

const venue = ref<Venue | null>(null)
const linkedItems = ref<Item[]>([])
const isEditing = ref(false)
const isDeleting = ref(false)
const isSaving = ref(false)

const editName = ref('')
const editAddress = ref('')
const editWebsite = ref('')
const editType = ref('other')
const editRating = ref(0)

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

onMounted(refreshVenue)

function resetEditFields(data: Venue) {
  editName.value = data.name
  editAddress.value = data.address ?? ''
  editWebsite.value = data.website ?? ''
  editType.value = data.type
  editRating.value = data.rating ?? 0
}

function startEditing() {
  if (venue.value) resetEditFields(venue.value)
  isEditing.value = true
}

async function saveEdits() {
  if (!venue.value) return
  isSaving.value = true
  try {
    const { data } = await venuesApi.update(venue.value.id, {
      name: editName.value.trim(),
      address: editAddress.value.trim() || undefined,
      website: editWebsite.value.trim() || undefined,
      type: editType.value,
      rating: editRating.value || undefined,
    })
    venue.value = data
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
    <button @click="router.back()" class="text-stone-400 hover:text-stone-200 text-sm mb-4 flex items-center gap-1">
      <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
      </svg>
      Back
    </button>

    <!-- Photos -->
    <div v-if="photoUrls.length" class="mb-4 -mx-4">
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
          <span class="text-xs px-2 py-0.5 rounded-full bg-stone-800 text-stone-400 capitalize">{{ venue.type }}</span>
          <h2 class="text-2xl font-bold text-stone-100 mt-2">{{ venue.name }}</h2>
        </div>

        <div v-if="venue.rating" class="flex items-center gap-2">
          <StarRating :rating="venue.rating" size="md" />
        </div>

        <div v-if="venue.address" class="text-stone-400 text-sm">{{ venue.address }}</div>

        <a v-if="venue.website" :href="venue.website" target="_blank" class="text-amber-500 text-sm hover:text-amber-400 block truncate">
          {{ venue.website }}
        </a>

        <div class="flex gap-2">
          <button
            @click="startEditing"
            class="flex-1 bg-amber-700 hover:bg-amber-600 text-white py-2.5 min-h-[44px] rounded-xl text-sm font-medium"
          >
            Edit
          </button>
          <button
            @click="deleteVenue"
            :disabled="isDeleting"
            class="px-4 py-2.5 min-h-[44px] bg-stone-800 text-red-400 hover:bg-stone-700 rounded-xl text-sm"
          >
            {{ isDeleting ? 'Deleting...' : 'Delete' }}
          </button>
        </div>
      </div>

      <!-- Linked items -->
      <div v-if="linkedItems.length" class="mt-6">
        <h3 class="text-sm font-medium text-stone-400 mb-3">Linked Items</h3>
        <div class="space-y-2">
          <router-link
            v-for="item in linkedItems"
            :key="item.id"
            :to="`/items/${item.id}`"
            class="block bg-stone-900 border border-stone-800 rounded-xl p-3 hover:border-stone-700 transition-colors"
          >
            <div class="flex items-center gap-3">
              <img
                v-if="item.photoUrls?.length"
                :src="item.photoUrls[0]"
                class="w-10 h-10 object-cover rounded-lg shrink-0"
              />
              <div class="flex-1 min-w-0">
                <span class="text-xs px-2 py-0.5 rounded-full bg-stone-800 text-stone-400">{{ item.type }}</span>
                <h4 class="font-medium text-stone-100 truncate text-sm">{{ item.name }}</h4>
              </div>
            </div>
          </router-link>
        </div>
      </div>
    </template>

    <!-- Edit mode -->
    <template v-else>
      <div class="space-y-4">
        <div>
          <label class="block text-sm text-stone-400 mb-1">Name</label>
          <input
            v-model="editName"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-2.5 text-stone-100 text-sm focus:outline-none focus:border-amber-700"
          />
        </div>

        <div>
          <label class="block text-sm text-stone-400 mb-1">Type</label>
          <select
            v-model="editType"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-2.5 text-stone-100 text-sm focus:outline-none focus:border-amber-700 appearance-none"
          >
            <option v-for="opt in venueTypeOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </div>

        <div>
          <label class="block text-sm text-stone-400 mb-1">Address</label>
          <input
            v-model="editAddress"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-2.5 text-stone-100 text-sm focus:outline-none focus:border-amber-700"
          />
        </div>

        <div>
          <label class="block text-sm text-stone-400 mb-1">Website</label>
          <input
            v-model="editWebsite"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-2.5 text-stone-100 text-sm focus:outline-none focus:border-amber-700"
          />
        </div>

        <div>
          <label class="block text-sm text-stone-400 mb-1">Rating</label>
          <StarRating :rating="editRating" size="lg" interactive @update:rating="editRating = $event" />
        </div>

        <div class="flex gap-2">
          <button
            @click="saveEdits"
            :disabled="isSaving"
            class="flex-1 bg-amber-700 hover:bg-amber-600 disabled:bg-stone-700 disabled:text-stone-500 text-white py-2.5 min-h-[44px] rounded-xl text-sm font-medium"
          >
            {{ isSaving ? 'Saving...' : 'Save' }}
          </button>
          <button
            @click="isEditing = false"
            class="px-4 py-2.5 min-h-[44px] bg-stone-800 text-stone-400 rounded-xl text-sm hover:bg-stone-700"
          >
            Cancel
          </button>
        </div>
      </div>
    </template>
  </div>
</template>
