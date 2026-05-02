<script setup lang="ts">
import { ref, inject, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useVenuesStore } from '../stores/venues'
import { RefreshKey } from '../composables/refreshKey'
import { useBreakpoint } from '../composables/useBreakpoint'
import { useVirtualizer } from '@tanstack/vue-virtual'
import StarRating from '../components/common/StarRating.vue'
import VenueLeaderboard from '../components/venues/VenueLeaderboard.vue'

const router = useRouter()
const venuesStore = useVenuesStore()
const registerRefresh = inject(RefreshKey)
const { isDesktop } = useBreakpoint()
const leaderboardSort = ref<'rating' | 'items'>('rating')

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
  { label: 'Cafe', value: 'cafe' },
  { label: 'Other', value: 'other' },
]

registerRefresh?.(async () => {
  await venuesStore.loadVenues(undefined, true)
})

onMounted(() => {
  venuesStore.loadVenues(undefined, true)
})

interface VenueGroup {
  title: string
  venues: typeof venuesStore.venues
}

const groupedVenues = computed<VenueGroup[]>(() => {
  const venues = venuesStore.venues

  // Separate venues with "to-try" label
  const toTryVenues = venues.filter(v => v.labels?.some(l => l.toLowerCase() === 'to-try'))
  const regularVenues = venues.filter(v => !v.labels?.some(l => l.toLowerCase() === 'to-try'))

  const groups: VenueGroup[] = []

  // Helper to create groups by type
  const createTypeGroups = (venueList: typeof venues, prefix: string) => {
    const typeMap = new Map<string, typeof venues>()

    venueList.forEach(venue => {
      const type = venue.type.toLowerCase()
      if (!typeMap.has(type)) {
        typeMap.set(type, [])
      }
      typeMap.get(type)!.push(venue)
    })

    // Sort each type group by rating (descending), then by updatedAt
    typeMap.forEach((typeVenues) => {
      typeVenues.sort((a, b) => {
        const ra = a.rating ?? 0
        const rb = b.rating ?? 0
        if (rb !== ra) return rb - ra
        return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
      })
    })

    // Create groups in specific order
    const typeOrder = ['bar', 'lounge', 'restaurant', 'cafe', 'other']
    typeOrder.forEach(type => {
      if (typeMap.has(type) && typeMap.get(type)!.length > 0) {
        const typeName = type.charAt(0).toUpperCase() + type.slice(1)
        groups.push({
          title: prefix ? `${prefix} - ${typeName}` : typeName,
          venues: typeMap.get(type)!
        })
      }
    })
  }

  // Add "to-try" groups first
  if (toTryVenues.length > 0) {
    createTypeGroups(toTryVenues, 'To Try')
  }

  // Add regular venue groups
  if (regularVenues.length > 0) {
    createTypeGroups(regularVenues, '')
  }

  return groups
})

const sortedVenues = computed(() => {
  // Flatten grouped venues for leaderboard
  return groupedVenues.value.flatMap(g => g.venues)
})

const leaderboardVenues = computed(() => {
  return sortedVenues.value.map(v => ({
    id: v.id,
    name: v.name,
    type: v.type,
    rating: v.rating ?? null,
    itemCount: 0,
  }))
})

const scrollContainerRef = ref<HTMLElement | null>(null)

// Flatten grouped venues with headers for virtual scrolling
interface VirtualItem {
  type: 'header' | 'venue'
  groupTitle?: string
  venue?: typeof venuesStore.venues[0]
}

const virtualItems = computed<VirtualItem[]>(() => {
  const items: VirtualItem[] = []
  groupedVenues.value.forEach(group => {
    items.push({ type: 'header', groupTitle: group.title })
    group.venues.forEach(venue => {
      items.push({ type: 'venue', venue })
    })
  })
  return items
})

const virtualizer = useVirtualizer(computed(() => ({
  count: virtualItems.value.length,
  getScrollElement: () => scrollContainerRef.value,
  estimateSize: (index) => virtualItems.value[index].type === 'header' ? 40 : 100,
  overscan: 5,
  gap: 12,
})))

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
  <div class="p-4 mx-auto" :class="isDesktop ? 'max-w-6xl' : 'max-w-lg'">
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

    <!-- Loading -->
    <div v-if="venuesStore.isLoading && !groupedVenues.length" class="text-[#96BEE6]/70 text-center py-12">
      Loading...
    </div>

    <!-- Empty -->
    <div v-else-if="!groupedVenues.length" class="text-[#96BEE6]/70 text-center py-12">
      <p>No venues yet. Add your favorite spots.</p>
    </div>

    <!-- Venue list (virtual scroll with groups) -->
    <div
      v-else
      ref="scrollContainerRef"
      class="virtual-list-container overflow-y-auto"
    >
      <div
        :style="{ height: `${virtualizer.getTotalSize()}px`, width: '100%', position: 'relative' }"
      >
        <div
          v-for="row in virtualizer.getVirtualItems()"
          :key="row.index"
          :data-index="row.index"
          :ref="(el: any) => { if (el?.$el || el) virtualizer.measureElement(el?.$el ?? el) }"
          :style="{ position: 'absolute', top: 0, left: 0, width: '100%', transform: `translateY(${row.start}px)` }"
        >
          <!-- Group Header -->
          <div v-if="virtualItems[row.index].type === 'header'" class="mb-3">
            <h3 class="text-sm font-semibold text-[#96BEE6] uppercase tracking-wide">
              {{ virtualItems[row.index].groupTitle }}
            </h3>
          </div>

          <!-- Venue Item -->
          <router-link
            v-else-if="virtualItems[row.index].venue"
            :to="`/venues/${virtualItems[row.index].venue!.id}`"
            class="block bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 hover:border-[#1e407c]/50 transition-colors"
          >
            <div class="flex items-start gap-3">
              <img
                v-if="virtualItems[row.index].venue!.photoUrls.length"
                :src="virtualItems[row.index].venue!.photoUrls[0]"
                class="w-16 h-16 object-cover rounded-lg shrink-0"
                loading="lazy"
              />
              <div v-else class="w-16 h-16 bg-[#0a2a52] rounded-lg shrink-0 flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6 text-[#4a7aa5]/60" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                  <path stroke-linecap="round" stroke-linejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
                </svg>
              </div>

              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2 mb-1">
                  <span class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6] capitalize">{{ virtualItems[row.index].venue!.type }}</span>
                </div>
                <h3 class="font-medium text-white truncate">{{ virtualItems[row.index].venue!.name }}</h3>
                <p v-if="virtualItems[row.index].venue!.address" class="text-sm text-[#96BEE6]/70 truncate">{{ virtualItems[row.index].venue!.address }}</p>
                <div v-if="virtualItems[row.index].venue!.rating" class="mt-1">
                  <StarRating :rating="virtualItems[row.index].venue!.rating!" size="sm" />
                </div>
                <div v-if="virtualItems[row.index].venue!.labels?.length" class="flex flex-wrap gap-1 mt-1.5">
                  <span
                    v-for="label in virtualItems[row.index].venue!.labels!.slice(0, 3)"
                    :key="label"
                    class="text-[10px] px-1.5 py-0.5 rounded-full bg-[#1e407c]/30 text-[#96BEE6]/80"
                  >
                    {{ label }}
                  </span>
                  <span v-if="virtualItems[row.index].venue!.labels!.length > 3" class="text-[10px] text-[#4a7aa5]/60">
                    +{{ virtualItems[row.index].venue!.labels!.length - 3 }}
                  </span>
                </div>
              </div>
            </div>
          </router-link>
        </div>
      </div>
    </div>

    <!-- Desktop: Leaderboard sidebar -->
    <VenueLeaderboard
      v-if="isDesktop && sortedVenues.length"
      :venues="leaderboardVenues"
      :sort-by="leaderboardSort"
      @sort="leaderboardSort = $event"
      @select="(id: string) => router.push(`/venues/${id}`)"
      class="mt-6"
    />

    <!-- Add Venue Modal -->
    <Teleport to="body">
      <Transition name="fade">
        <div v-if="showAddForm" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60" @click.self="resetForm()">
          <div class="bg-[#041e3e] border border-[#1e407c] rounded-2xl p-6 w-full max-w-sm shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <h3 class="text-lg font-semibold text-white">Add Venue</h3>
              <button @click="resetForm()" class="text-[#4a7aa5] hover:text-white p-1">
                <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

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

              <button
                @click="addVenue"
                :disabled="isAdding || !newName.trim()"
                class="w-full bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#0a2a52] disabled:text-[#4a7aa5]/60 text-white py-3 rounded-xl font-medium transition-colors"
              >
                {{ isAdding ? 'Adding...' : 'Add Venue' }}
              </button>
            </template>

            <!-- URL form -->
            <template v-else>
              <p class="text-xs text-[#5a8ab5]">Paste an Apple Maps, Google Maps, or venue website URL</p>
              <input
                v-model="newUrl"
                placeholder="https://maps.apple.com/..."
                class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
              />
              <p v-if="urlError" class="text-xs text-red-400">{{ urlError }}</p>

              <button
                @click="addVenueFromUrl"
                :disabled="isAdding || !newUrl.trim()"
                class="w-full bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#0a2a52] disabled:text-[#4a7aa5]/60 text-white py-3 rounded-xl font-medium transition-colors"
              >
                {{ isAdding ? 'Extracting...' : 'Extract Venue' }}
              </button>
            </template>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
.virtual-list-container {
  height: calc(100vh - 200px);
  height: calc(100dvh - 200px);
}
</style>
