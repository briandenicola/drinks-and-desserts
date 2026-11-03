<script setup lang="ts">
import { ref, inject, onMounted, onUnmounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useVenuesStore } from '../stores/venues'
import { useAuthStore } from '../stores/auth'
import { usePwa } from '../composables/usePwa'
import { RefreshKey } from '../composables/refreshKey'
import { useBreakpoint } from '../composables/useBreakpoint'
import { useVirtualizer } from '@tanstack/vue-virtual'
import StarRating from '../components/common/StarRating.vue'
import VenueLeaderboard from '../components/venues/VenueLeaderboard.vue'
import type { Venue } from '../services/venues'

const router = useRouter()
const venuesStore = useVenuesStore()
const auth = useAuthStore()
const registerRefresh = inject(RefreshKey)
const { isDesktop } = useBreakpoint()
const { isPwa } = usePwa()
const leaderboardSort = ref<'rating' | 'items'>('rating')

function getDefaultSortDirection(sort: string): 'asc' | 'desc' {
  return sort === 'type' ? 'asc' : 'desc'
}

const defaultFilter = auth.user?.preferences?.venueFilter || undefined
const defaultSort = auth.user?.preferences?.venueSort || 'rating'
const defaultSortDirection = auth.user?.preferences?.venueSortDirection || getDefaultSortDirection(defaultSort)

if (isPwa.value) {
  if (!venuesStore.viewSort) {
    venuesStore.activeFilter = defaultFilter
    venuesStore.viewSort = defaultSort
    venuesStore.viewSortDirection = defaultSortDirection
  }
} else {
  venuesStore.activeFilter = defaultFilter
  venuesStore.viewSort = defaultSort
  venuesStore.viewSortDirection = defaultSortDirection
  venuesStore.viewGroupBy = undefined
  venuesStore.searchQuery = ''
}

const activeSort = computed({
  get: () => venuesStore.viewSort || 'rating',
  set: (v) => { venuesStore.viewSort = v }
})
const activeSortDirection = computed({
  get: () => venuesStore.viewSortDirection,
  set: (v) => { venuesStore.viewSortDirection = v }
})
const activeGroupBy = computed({
  get: () => venuesStore.viewGroupBy,
  set: (v) => { venuesStore.viewGroupBy = v }
})
const activeFilter = computed({
  get: () => venuesStore.activeFilter,
  set: (v) => { venuesStore.activeFilter = v }
})
const searchQuery = computed({
  get: () => venuesStore.searchQuery,
  set: (v) => { venuesStore.searchQuery = v }
})

const showActionMenu = ref(false)

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
const sortOptions = [
  { label: 'Rating', value: 'rating' },
  { label: 'Type', value: 'type' },
  { label: 'Added', value: 'createdAt' },
  { label: 'Updated', value: 'updatedAt' },
]
const sortDirectionOptions = [
  { label: 'Ascending', value: 'asc' as const },
  { label: 'Descending', value: 'desc' as const },
]
const groupByOptions = [
  { label: 'None', value: undefined },
  { label: 'Type', value: 'type' },
]
const venueTypeFilters = [{ label: 'All', value: undefined }, ...venueTypeOptions]
registerRefresh?.(async () => {
  await venuesStore.loadVenues(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
})

onMounted(async () => {
  // In PWA mode, skip load if already initialized (preserves state across navigation)
  const skipIfLoaded = isPwa.value
  await venuesStore.loadVenues(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value, skipIfLoaded)

  // Restore scroll position in PWA mode
  if (skipIfLoaded && venuesStore.hasInitialLoad) {
    requestAnimationFrame(() => {
      virtualizer.value.scrollToOffset(venuesStore.scrollPosition, { behavior: 'auto' })
    })
  }

  document.addEventListener('click', closeActionMenu)
})

onUnmounted(() => {
  // Save scroll position in PWA mode before unmount
  if (isPwa.value) {
    venuesStore.scrollPosition = virtualizer.value.scrollOffset ?? 0
  }

  document.removeEventListener('click', closeActionMenu)
})

const sortedVenues = computed(() => {
  // Server handles primary sorting; client-side is fallback only for display consistency
  return venuesStore.venues
})

const normalizedSearch = computed(() => searchQuery.value.trim().toLowerCase())

function matchesVenueSearch(venue: Venue, search: string): boolean {
  if (!search) return true
  const fields = [venue.name, venue.address, venue.website, venue.type]
  if (fields.some(field => field?.toLowerCase().includes(search))) {
    return true
  }
  if (venue.labels?.some(label => label.toLowerCase().includes(search))) {
    return true
  }
  return false
}

const searchFilteredVenues = computed(() => {
  const search = normalizedSearch.value
  if (!search) return sortedVenues.value
  return sortedVenues.value.filter(venue => matchesVenueSearch(venue, search))
})

const leaderboardVenues = computed(() => {
  return searchFilteredVenues.value.map(v => ({
    id: v.id,
    name: v.name,
    type: v.type,
    rating: v.rating ?? null,
    itemCount: 0,
  }))
})

const scrollContainerRef = ref<HTMLElement | null>(null)

interface VirtualItem {
  venue: typeof venuesStore.venues[0]
}

const virtualItems = computed<VirtualItem[]>(() => {
  return searchFilteredVenues.value.map(venue => ({ venue }))
})

const virtualizer = useVirtualizer(computed(() => ({
  count: virtualItems.value.length,
  getScrollElement: () => scrollContainerRef.value,
  estimateSize: () => 100,
  overscan: 5,
  gap: 12,
})))

const isLoadingMore = ref(false)
const lastVirtualIndex = computed(() => {
  const rows = virtualizer.value.getVirtualItems()
  return rows.length ? rows[rows.length - 1].index : -1
})

async function maybeLoadMore() {
  if (isLoadingMore.value || lastVirtualIndex.value < 0) return
  if (virtualItems.value.length - 1 - lastVirtualIndex.value > 3) return
  if (!venuesStore.continuationToken || venuesStore.isLoading) return

  isLoadingMore.value = true
  try {
    await venuesStore.loadVenues(activeFilter.value, false, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  } finally {
    isLoadingMore.value = false
  }
}

watch(lastVirtualIndex, () => {
  void maybeLoadMore()
})

watch(searchQuery, () => {
  virtualizer.value.scrollToOffset(0)
})

function closeActionMenu(e: MouseEvent) {
  const target = e.target as HTMLElement
  if (!target.closest('.action-menu-dropdown')) {
    showActionMenu.value = false
  }
}

async function setSort(value: string) {
  activeSort.value = value
  showActionMenu.value = false
  await venuesStore.loadVenues(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  virtualizer.value.scrollToOffset(0)
}

async function setSortDirection(value: 'asc' | 'desc') {
  activeSortDirection.value = value
  showActionMenu.value = false
  await venuesStore.loadVenues(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  virtualizer.value.scrollToOffset(0)
}

async function setGroupBy(value?: string) {
  activeGroupBy.value = value
  showActionMenu.value = false
  await venuesStore.loadVenues(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  virtualizer.value.scrollToOffset(0)
}

async function setFilter(value?: string) {
  activeFilter.value = value
  showActionMenu.value = false
  await venuesStore.loadVenues(value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  virtualizer.value.scrollToOffset(0)
}

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
      <h2 class="text-xl font-bold text-th-heading">Venues</h2>
      <button
        @click="showAddForm = !showAddForm"
        class="text-th-accent hover:text-th-text transition-colors"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
        </svg>
      </button>
    </div>

    <div class="flex items-center gap-2 mb-4">
      <div class="relative flex-1">
        <input
          v-model="searchQuery"
          placeholder="Search venues..."
          class="w-full bg-th-inset border border-th-edge/50 rounded-xl pl-10 pr-3 py-2.5 text-sm text-th-text placeholder-th-muted focus:outline-none focus:border-th-edge"
        />
        <svg xmlns="http://www.w3.org/2000/svg" class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-th-muted" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-4.35-4.35m1.85-5.15a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
      </div>

      <div class="relative action-menu-dropdown shrink-0">
        <button
          @click="showActionMenu = !showActionMenu"
          class="h-[44px] w-[44px] bg-th-inset border border-th-edge/50 rounded-xl text-th-accent hover:border-th-edge transition-colors flex items-center justify-center"
          aria-label="Open actions menu"
          title="Actions"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 6h9.75M3.75 6h3.75m3 12h9.75m-16.5 0h3.75m0-12a1.5 1.5 0 1 0-3 0 1.5 1.5 0 0 0 3 0Zm6 12a1.5 1.5 0 1 0-3 0 1.5 1.5 0 0 0 3 0Zm0-12h4.5m-4.5 12h4.5" />
          </svg>
        </button>

        <div
          v-if="showActionMenu"
          class="absolute right-0 top-full mt-1 w-72 max-h-[70vh] overflow-y-auto bg-th-inset border border-th-edge/50 rounded-xl shadow-lg z-20 p-3 space-y-4"
        >
          <div>
            <p class="text-[11px] uppercase tracking-wide text-th-muted mb-2">Filter</p>
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="opt in venueTypeFilters"
                :key="opt.label"
                @click="setFilter(opt.value)"
                class="px-3 py-2 rounded-lg text-xs border text-left transition-colors"
                :class="activeFilter === opt.value
                  ? 'bg-th-brand border-th-edge text-th-on-brand'
                  : 'bg-th-card border-th-edge/50 text-th-accent'"
              >
                {{ opt.label }}
              </button>
            </div>
          </div>

          <div>
            <p class="text-[11px] uppercase tracking-wide text-th-muted mb-2">Sort</p>
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="opt in sortOptions"
                :key="opt.value"
                @click="setSort(opt.value)"
                class="px-3 py-2 rounded-lg text-xs border text-left transition-colors"
                :class="activeSort === opt.value
                  ? 'bg-th-brand border-th-edge text-th-on-brand'
                  : 'bg-th-card border-th-edge/50 text-th-accent'"
              >
                {{ opt.label }}
              </button>
            </div>
          </div>

          <div>
            <p class="text-[11px] uppercase tracking-wide text-th-muted mb-2">Sort Direction</p>
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="opt in sortDirectionOptions"
                :key="opt.value"
                @click="setSortDirection(opt.value)"
                class="px-3 py-2 rounded-lg text-xs border text-left transition-colors"
                :class="activeSortDirection === opt.value
                  ? 'bg-th-brand border-th-edge text-th-on-brand'
                  : 'bg-th-card border-th-edge/50 text-th-accent'"
              >
                {{ opt.label }}
              </button>
            </div>
          </div>

          <div>
            <p class="text-[11px] uppercase tracking-wide text-th-muted mb-2">Group By</p>
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="opt in groupByOptions"
                :key="opt.label"
                @click="setGroupBy(opt.value)"
                class="px-3 py-2 rounded-lg text-xs border text-left transition-colors"
                :class="activeGroupBy === opt.value
                  ? 'bg-th-brand border-th-edge text-th-on-brand'
                  : 'bg-th-card border-th-edge/50 text-th-accent'"
              >
                {{ opt.label }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="venuesStore.isLoading && !virtualItems.length" class="text-th-accent/70 text-center py-12">
      Loading...
    </div>

    <!-- Empty -->
    <div v-else-if="!virtualItems.length" class="text-th-accent/70 text-center py-12">
      <p v-if="normalizedSearch">No results for "{{ searchQuery.trim() }}".</p>
      <p v-else>No venues yet. Add your favorite spots.</p>
    </div>

    <!-- Venue list (virtual scroll) -->
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
          <!-- Venue Item -->
          <router-link
            v-if="virtualItems[row.index].venue"
            :to="`/venues/${virtualItems[row.index].venue!.id}`"
            class="block bg-th-inset border border-th-line rounded-xl p-4 hover:border-th-edge/50 transition-colors"
          >
            <div class="flex items-start gap-3">
              <img
                v-if="virtualItems[row.index].venue!.photoUrls.length"
                :src="virtualItems[row.index].venue!.photoUrls[0]"
                class="w-16 h-16 object-cover rounded-lg shrink-0"
                loading="lazy"
              />
              <div v-else class="w-16 h-16 bg-th-card rounded-lg shrink-0 flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6 text-th-muted/60" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                  <path stroke-linecap="round" stroke-linejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
                </svg>
              </div>

              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2 mb-1">
                  <span class="text-xs px-2 py-0.5 rounded-full bg-th-card text-th-accent capitalize">{{ virtualItems[row.index].venue!.type }}</span>
                </div>
                <h3 class="font-medium text-th-heading truncate">{{ virtualItems[row.index].venue!.name }}</h3>
                <p v-if="virtualItems[row.index].venue!.address" class="text-sm text-th-accent/70 truncate">{{ virtualItems[row.index].venue!.address }}</p>
                <div v-if="virtualItems[row.index].venue!.rating" class="mt-1">
                  <StarRating :rating="virtualItems[row.index].venue!.rating!" size="sm" />
                </div>
                <div v-if="virtualItems[row.index].venue!.labels?.length" class="flex flex-wrap gap-1 mt-1.5">
                  <span
                    v-for="label in virtualItems[row.index].venue!.labels!.slice(0, 3)"
                    :key="label"
                    class="text-[10px] px-1.5 py-0.5 rounded-full bg-th-brand/30 text-th-accent/80"
                  >
                    {{ label }}
                  </span>
                  <span v-if="virtualItems[row.index].venue!.labels!.length > 3" class="text-[10px] text-th-muted/60">
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
      v-if="isDesktop && searchFilteredVenues.length"
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
          <div class="bg-th-inset border border-th-edge rounded-2xl p-6 w-full max-w-sm shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <h3 class="text-lg font-semibold text-th-heading">Add Venue</h3>
              <button @click="resetForm()" class="text-th-muted hover:text-th-text p-1">
                <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

            <!-- Mode toggle -->
            <div class="flex rounded-lg bg-th-card p-0.5">
              <button
                @click="addMode = 'manual'"
                :class="[
                  'flex-1 py-1.5 text-xs font-medium rounded-md transition-colors',
                  addMode === 'manual' ? 'bg-th-brand text-th-on-brand' : 'text-th-accent hover:text-th-text/80'
                ]"
              >
                Manual
              </button>
              <button
                @click="addMode = 'url'"
                :class="[
                  'flex-1 py-1.5 text-xs font-medium rounded-md transition-colors',
                  addMode === 'url' ? 'bg-th-brand text-th-on-brand' : 'text-th-accent hover:text-th-text/80'
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
                class="w-full bg-th-card border border-th-edge/50 rounded-xl px-4 py-2.5 text-th-text text-sm placeholder-th-muted focus:outline-none focus:border-th-edge"
              />

              <select
                v-model="newType"
                class="w-full bg-th-card border border-th-edge/50 rounded-xl px-4 py-2.5 text-th-text text-sm focus:outline-none focus:border-th-edge appearance-none"
              >
                <option v-for="opt in venueTypeOptions" :key="opt.value" :value="opt.value">
                  {{ opt.label }}
                </option>
              </select>

              <input
                v-model="newAddress"
                placeholder="Address (optional)"
                class="w-full bg-th-card border border-th-edge/50 rounded-xl px-4 py-2.5 text-th-text text-sm placeholder-th-muted focus:outline-none focus:border-th-edge"
              />

              <input
                v-model="newWebsite"
                placeholder="Website (optional)"
                class="w-full bg-th-card border border-th-edge/50 rounded-xl px-4 py-2.5 text-th-text text-sm placeholder-th-muted focus:outline-none focus:border-th-edge"
              />

              <button
                @click="addVenue"
                :disabled="isAdding || !newName.trim()"
                class="w-full bg-th-brand hover:bg-th-brand-hover disabled:bg-th-card disabled:text-th-muted text-th-on-brand py-3 rounded-xl font-medium transition-colors"
              >
                {{ isAdding ? 'Adding...' : 'Add Venue' }}
              </button>
            </template>

            <!-- URL form -->
            <template v-else>
              <p class="text-xs text-th-muted-strong">Paste an Apple Maps, Google Maps, or venue website URL</p>
              <input
                v-model="newUrl"
                placeholder="https://maps.apple.com/..."
                class="w-full bg-th-card border border-th-edge/50 rounded-xl px-4 py-2.5 text-th-text text-sm placeholder-th-muted focus:outline-none focus:border-th-edge"
              />
              <p v-if="urlError" class="text-xs text-th-danger">{{ urlError }}</p>

              <button
                @click="addVenueFromUrl"
                :disabled="isAdding || !newUrl.trim()"
                class="w-full bg-th-brand hover:bg-th-brand-hover disabled:bg-th-card disabled:text-th-muted text-th-on-brand py-3 rounded-xl font-medium transition-colors"
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
