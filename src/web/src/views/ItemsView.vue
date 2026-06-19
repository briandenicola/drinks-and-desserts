<script setup lang="ts">
import { ref, inject, onMounted, onUnmounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useItemsStore } from '../stores/items'
import { useAuthStore } from '../stores/auth'
import { usePwa } from '../composables/usePwa'
import { RefreshKey } from '../composables/refreshKey'
import { getErrorMessage } from '../services/errors'
import { useVirtualizer } from '@tanstack/vue-virtual'
import StarRating from '../components/common/StarRating.vue'
import { useBreakpoint } from '../composables/useBreakpoint'
import FilterSidebar from '../components/collection/FilterSidebar.vue'
import type { CollectionFilters } from '../components/collection/FilterSidebar.vue'
import CollectionGrid from '../components/collection/CollectionGrid.vue'
import DetailPanel from '../components/collection/DetailPanel.vue'
import type { Item } from '../services/items'

const { isDesktop } = useBreakpoint()
const { isPwa } = usePwa()

const router = useRouter()
const itemsStore = useItemsStore()
const auth = useAuthStore()

function getDefaultSortDirection(sort: string): 'asc' | 'desc' {
  return sort === 'type' ? 'asc' : 'desc'
}

const defaultFilter = auth.user?.preferences?.collectionFilter || undefined
const defaultSort = auth.user?.preferences?.collectionSort || 'rating'
const defaultSortDirection = auth.user?.preferences?.collectionSortDirection || getDefaultSortDirection(defaultSort)

if (isPwa.value) {
  if (!itemsStore.viewSort) {
    itemsStore.activeFilter = defaultFilter
    itemsStore.viewSort = defaultSort
    itemsStore.viewSortDirection = defaultSortDirection
  }
} else {
  if (!itemsStore.activeFilter && defaultFilter) itemsStore.activeFilter = defaultFilter
  itemsStore.viewSort = defaultSort
  itemsStore.viewSortDirection = defaultSortDirection
  itemsStore.viewGroupBy = undefined
  itemsStore.searchQuery = ''
}

const activeFilter = computed({
  get: () => itemsStore.activeFilter,
  set: (v) => { itemsStore.activeFilter = v }
})
const activeTab = computed({
  get: () => itemsStore.activeTab,
  set: (v) => { itemsStore.activeTab = v }
})
const activeSort = computed({
  get: () => itemsStore.viewSort || 'rating',
  set: (v) => { itemsStore.viewSort = v }
})
const activeSortDirection = computed({
  get: () => itemsStore.viewSortDirection,
  set: (v) => { itemsStore.viewSortDirection = v }
})
const activeGroupBy = computed({
  get: () => itemsStore.viewGroupBy,
  set: (v) => { itemsStore.viewGroupBy = v }
})
const searchQuery = computed({
  get: () => itemsStore.searchQuery,
  set: (v) => { itemsStore.searchQuery = v }
})

const registerRefresh = inject(RefreshKey)

// Wishlist add form
const showAddForm = ref(false)
const showActionMenu = ref(false)
const newName = ref('')
const newType = ref('whiskey')
const newBrand = ref('')
const newNotes = ref('')
const isAdding = ref(false)
const newUrl = ref('')
const isExtractingUrl = ref(false)
const urlError = ref('')

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

registerRefresh?.(async () => {
  if (activeTab.value === 'wishlist') {
    await itemsStore.loadWishlist(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  } else {
    await itemsStore.loadItems(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  }
})

const typeFilters = [
  { label: 'All', value: undefined },
  { label: 'Whiskey', value: 'whiskey' },
  { label: 'Wine', value: 'wine' },
  { label: 'Cocktail', value: 'cocktail' },
  { label: 'Vodka', value: 'vodka' },
  { label: 'Gin', value: 'gin' },
  { label: 'Espresso', value: 'espresso' },
  { label: 'Latte', value: 'latte' },
  { label: 'Cappuccino', value: 'cappuccino' },
  { label: 'Cold Brew', value: 'cold-brew' },
  { label: 'Pour Over', value: 'pour-over' },
  { label: 'Coffee', value: 'coffee' },
  { label: 'Cigar', value: 'cigar' },
  { label: 'Dessert', value: 'dessert' },
  { label: 'Custom', value: 'custom' },
]

const typeOptions = [
  { label: 'Whiskey', value: 'whiskey' },
  { label: 'Wine', value: 'wine' },
  { label: 'Cocktail', value: 'cocktail' },
  { label: 'Vodka', value: 'vodka' },
  { label: 'Gin', value: 'gin' },
  { label: 'Espresso', value: 'espresso' },
  { label: 'Latte', value: 'latte' },
  { label: 'Cappuccino', value: 'cappuccino' },
  { label: 'Cold Brew', value: 'cold-brew' },
  { label: 'Pour Over', value: 'pour-over' },
  { label: 'Coffee', value: 'coffee' },
  { label: 'Cigar', value: 'cigar' },
  { label: 'Dessert', value: 'dessert' },
  { label: 'Custom', value: 'custom' },
]

async function setSort(value: string) {
  activeSort.value = value
  showActionMenu.value = false
  if (activeTab.value === 'wishlist') {
    await itemsStore.loadWishlist(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  } else {
    await itemsStore.loadItems(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  }
  virtualizer.value.scrollToOffset(0)
}

async function setSortDirection(value: 'asc' | 'desc') {
  activeSortDirection.value = value
  showActionMenu.value = false
  if (activeTab.value === 'wishlist') {
    await itemsStore.loadWishlist(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  } else {
    await itemsStore.loadItems(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  }
  virtualizer.value.scrollToOffset(0)
}

async function setGroupBy(value?: string) {
  activeGroupBy.value = value
  showActionMenu.value = false
  if (activeTab.value === 'wishlist') {
    await itemsStore.loadWishlist(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  } else {
    await itemsStore.loadItems(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  }
  virtualizer.value.scrollToOffset(0)
}

function setFilter(value?: string) {
  activeFilter.value = value
  showActionMenu.value = false
  if (activeTab.value === 'wishlist') {
    itemsStore.loadWishlist(value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  } else {
    itemsStore.loadItems(value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  }
}

function switchTab(tab: 'collection' | 'wishlist') {
  activeTab.value = tab
  const defaultFilter = auth.user?.preferences?.collectionFilter || undefined
  activeFilter.value = defaultFilter
  showActionMenu.value = false
  if (tab === 'wishlist') {
    itemsStore.loadWishlist(defaultFilter, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  } else {
    itemsStore.loadItems(defaultFilter, true, activeSort.value, activeSortDirection.value, activeGroupBy.value)
  }
}

async function addWishlistItem() {
  if (!newName.value.trim()) return
  isAdding.value = true
  try {
    await itemsStore.createWishlistItem({
      name: newName.value.trim(),
      type: newType.value,
      brand: newBrand.value.trim() || undefined,
      notes: newNotes.value.trim() || undefined,
    })
    newName.value = ''
    newBrand.value = ''
    newNotes.value = ''
    showAddForm.value = false
  } finally {
    isAdding.value = false
  }
}

async function addWishlistFromUrl() {
  if (!newUrl.value.trim()) return
  isExtractingUrl.value = true
  urlError.value = ''
  try {
    await itemsStore.createWishlistFromUrl(newUrl.value.trim())
    newUrl.value = ''
    showAddForm.value = false
  } catch (e: unknown) {
    urlError.value = getErrorMessage(e, 'Failed to submit URL')
  } finally {
    isExtractingUrl.value = false
  }
}

async function convertItem(id: string) {
  const item = await itemsStore.convertWishlistItem(id)
  router.push(`/items/${item.id}`)
}

async function deleteItem(id: string) {
  await itemsStore.deleteItem(id)
}

const displayItems = computed(() => {
  // Server handles primary sorting; client-side is fallback only for display consistency
  return activeTab.value === 'wishlist' ? itemsStore.wishlistItems : itemsStore.items
})

const normalizedSearch = computed(() => searchQuery.value.trim().toLowerCase())

function matchesItemSearch(item: Item, search: string): boolean {
  if (!search) return true
  const fields = [
    item.name,
    item.brand,
    item.type,
    item.userNotes,
    item.venue?.name,
  ]
  if (fields.some(field => field?.toLowerCase().includes(search))) {
    return true
  }
  if (item.tags.some(tag => tag.toLowerCase().includes(search))) {
    return true
  }
  return false
}

const searchFilteredItems = computed(() => {
  const search = normalizedSearch.value
  if (!search) return displayItems.value
  return displayItems.value.filter(item => matchesItemSearch(item, search))
})

const scrollContainerRef = ref<HTMLElement | null>(null)

interface VirtualItem {
  item: typeof itemsStore.items[0]
}

const virtualItems = computed<VirtualItem[]>(() => {
  return searchFilteredItems.value.map(item => ({ item }))
})

const virtualizer = useVirtualizer(computed(() => ({
  count: virtualItems.value.length,
  getScrollElement: () => scrollContainerRef.value,
  estimateSize: () => activeTab.value === 'wishlist' ? 140 : 100,
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

  const isWishlist = activeTab.value === 'wishlist'
  const hasMore = isWishlist
    ? !!itemsStore.wishlistContinuationToken
    : !!itemsStore.continuationToken
  const isLoading = isWishlist
    ? itemsStore.isLoadingWishlist
    : itemsStore.isLoading

  if (!hasMore || isLoading) return

  isLoadingMore.value = true
  try {
    if (isWishlist) {
      await itemsStore.loadWishlist(activeFilter.value, false, activeSort.value, activeSortDirection.value, activeGroupBy.value)
    } else {
      await itemsStore.loadItems(activeFilter.value, false, activeSort.value, activeSortDirection.value, activeGroupBy.value)
    }
  } finally {
    isLoadingMore.value = false
  }
}

watch([activeTab, activeFilter, searchQuery], () => {
  virtualizer.value.scrollToOffset(0)
})
watch(lastVirtualIndex, () => {
  void maybeLoadMore()
})

const isLoadingList = computed(() =>
  activeTab.value === 'wishlist' ? itemsStore.isLoadingWishlist : itemsStore.isLoading
)

function closeActionMenu(e: MouseEvent) {
  const target = e.target as HTMLElement
  if (!target.closest('.action-menu-dropdown')) {
    showActionMenu.value = false
  }
}

onMounted(() => {
  // In PWA mode, skip load if already initialized (preserves state across navigation)
  const skipIfLoaded = isPwa.value
  if (activeTab.value === 'wishlist') {
    itemsStore.loadWishlist(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value, skipIfLoaded)
  } else {
    itemsStore.loadItems(activeFilter.value, true, activeSort.value, activeSortDirection.value, activeGroupBy.value, skipIfLoaded)
  }

  // Restore scroll position in PWA mode
  if (skipIfLoaded) {
    requestAnimationFrame(() => {
      const scrollPos = activeTab.value === 'wishlist'
        ? itemsStore.wishlistScrollPosition
        : itemsStore.scrollPosition
      virtualizer.value.scrollToOffset(scrollPos, { behavior: 'auto' })
    })
  }

  document.addEventListener('click', closeActionMenu)
})

onUnmounted(() => {
  // Save scroll position in PWA mode before unmount
  if (isPwa.value) {
    const currentScrollPos = virtualizer.value.scrollOffset ?? 0
    if (activeTab.value === 'wishlist') {
      itemsStore.wishlistScrollPosition = currentScrollPos
    } else {
      itemsStore.scrollPosition = currentScrollPos
    }
  }

  document.removeEventListener('click', closeActionMenu)
})

// Desktop collection state
const selectedItemId = ref<string | null>(null)
const desktopFilters = ref<CollectionFilters>({ category: undefined, minRating: 0, labels: '' })

const desktopFilteredItems = computed(() => {
  const f = desktopFilters.value
  return searchFilteredItems.value.filter(item => {
    if (f.category && item.type !== f.category) return false
    if (f.minRating > 0 && (item.userRating ?? 0) < f.minRating) return false
    if (f.labels.trim()) {
      const labelSearch = f.labels.toLowerCase()
      if (!item.tags.some(t => t.toLowerCase().includes(labelSearch))) return false
    }
    return true
  })
})

function selectItem(id: string) {
  selectedItemId.value = selectedItemId.value === id ? null : id
}

function navigateToItem(id: string) {
  router.push(`/items/${id}`)
}
</script>

<template>
  <!-- Desktop layout (>= 1024px) -->
  <template v-if="isDesktop">
    <div class="flex h-[calc(100vh-0px)]">
      <FilterSidebar v-model="desktopFilters" />
      <CollectionGrid
        :items="desktopFilteredItems"
        :selected-id="selectedItemId"
        @select="selectItem"
      />
      <DetailPanel
        v-if="selectedItemId"
        :item-id="selectedItemId"
        @close="selectedItemId = null"
        @navigate="navigateToItem"
      />
    </div>
  </template>

  <!-- Mobile layout (< 1024px) -->
  <template v-else>
  <div class="p-4 max-w-lg mx-auto">
    <div class="mb-4">
      <h2 class="text-xl font-bold text-white">Collections</h2>
    </div>

    <!-- Search + Action Menu -->
    <div class="flex items-center gap-2 mb-4">
      <div class="relative flex-1">
        <input
          v-model="searchQuery"
          :placeholder="activeTab === 'wishlist' ? 'Search wishlist...' : 'Search collection...'"
          class="w-full bg-[#041e3e] border border-[#1e407c]/50 rounded-xl pl-10 pr-3 py-2.5 text-sm text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
        />
        <svg xmlns="http://www.w3.org/2000/svg" class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#4a7aa5]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-4.35-4.35m1.85-5.15a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
      </div>

      <div class="relative action-menu-dropdown shrink-0">
        <button
          @click="showActionMenu = !showActionMenu"
          class="h-[44px] w-[44px] bg-[#041e3e] border border-[#1e407c]/50 rounded-xl text-[#96BEE6] hover:border-[#1e407c] transition-colors flex items-center justify-center"
          aria-label="Open actions menu"
          title="Actions"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 6h9.75M3.75 6h3.75m3 12h9.75m-16.5 0h3.75m0-12a1.5 1.5 0 1 0-3 0 1.5 1.5 0 0 0 3 0Zm6 12a1.5 1.5 0 1 0-3 0 1.5 1.5 0 0 0 3 0Zm0-12h4.5m-4.5 12h4.5" />
          </svg>
        </button>

        <div
          v-if="showActionMenu"
          class="absolute right-0 top-full mt-1 w-72 max-h-[70vh] overflow-y-auto bg-[#041e3e] border border-[#1e407c]/50 rounded-xl shadow-lg z-20 p-3 space-y-4"
        >
          <div>
            <p class="text-[11px] uppercase tracking-wide text-[#4a7aa5] mb-2">Collection & Wishlist</p>
            <div class="grid grid-cols-2 gap-2">
              <button
                @click="switchTab('collection')"
                class="px-3 py-2 rounded-lg text-sm border transition-colors"
                :class="activeTab === 'collection'
                  ? 'bg-[#1e407c] border-[#1e407c] text-white'
                  : 'bg-[#0a2a52] border-[#1e407c]/50 text-[#96BEE6]'"
              >
                Collection
              </button>
              <button
                @click="switchTab('wishlist')"
                class="px-3 py-2 rounded-lg text-sm border transition-colors"
                :class="activeTab === 'wishlist'
                  ? 'bg-[#1e407c] border-[#1e407c] text-white'
                  : 'bg-[#0a2a52] border-[#1e407c]/50 text-[#96BEE6]'"
              >
                Wishlist
              </button>
            </div>
          </div>

          <div>
            <p class="text-[11px] uppercase tracking-wide text-[#4a7aa5] mb-2">Filter</p>
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="opt in typeFilters"
                :key="opt.label"
                @click="setFilter(opt.value)"
                class="px-3 py-2 rounded-lg text-xs border text-left transition-colors"
                :class="activeFilter === opt.value
                  ? 'bg-[#1e407c] border-[#1e407c] text-white'
                  : 'bg-[#0a2a52] border-[#1e407c]/50 text-[#96BEE6]'"
              >
                {{ opt.label }}
              </button>
            </div>
          </div>

          <div>
            <p class="text-[11px] uppercase tracking-wide text-[#4a7aa5] mb-2">Sort</p>
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="opt in sortOptions"
                :key="opt.value"
                @click="setSort(opt.value)"
                class="px-3 py-2 rounded-lg text-xs border text-left transition-colors"
                :class="activeSort === opt.value
                  ? 'bg-[#1e407c] border-[#1e407c] text-white'
                  : 'bg-[#0a2a52] border-[#1e407c]/50 text-[#96BEE6]'"
              >
                {{ opt.label }}
              </button>
            </div>
          </div>

          <div>
            <p class="text-[11px] uppercase tracking-wide text-[#4a7aa5] mb-2">Sort Direction</p>
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="opt in sortDirectionOptions"
                :key="opt.value"
                @click="setSortDirection(opt.value)"
                class="px-3 py-2 rounded-lg text-xs border text-left transition-colors"
                :class="activeSortDirection === opt.value
                  ? 'bg-[#1e407c] border-[#1e407c] text-white'
                  : 'bg-[#0a2a52] border-[#1e407c]/50 text-[#96BEE6]'"
              >
                {{ opt.label }}
              </button>
            </div>
          </div>

          <div>
            <p class="text-[11px] uppercase tracking-wide text-[#4a7aa5] mb-2">Group By</p>
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="opt in groupByOptions"
                :key="opt.label"
                @click="setGroupBy(opt.value)"
                class="px-3 py-2 rounded-lg text-xs border text-left transition-colors"
                :class="activeGroupBy === opt.value
                  ? 'bg-[#1e407c] border-[#1e407c] text-white'
                  : 'bg-[#0a2a52] border-[#1e407c]/50 text-[#96BEE6]'"
              >
                {{ opt.label }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Wishlist: Add button + form -->
    <div v-if="activeTab === 'wishlist'" class="mb-4">
      <button
        v-if="!showAddForm"
        @click="showAddForm = true"
        class="w-full bg-[#041e3e] border border-dashed border-[#1e407c]/50 rounded-xl p-3 text-[#96BEE6] hover:border-[#96BEE6]/50 hover:text-white/80 transition-colors text-sm"
      >
        + Add to wishlist
      </button>

      <div v-else class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
        <!-- URL extraction -->
        <div class="space-y-2">
          <div class="flex gap-2">
            <input
              v-model="newUrl"
              placeholder="Paste a product URL to auto-fill..."
              class="flex-1 bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
            />
            <button
              @click="addWishlistFromUrl"
              :disabled="isExtractingUrl || !newUrl.trim()"
              class="shrink-0 px-4 py-2.5 bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#1e407c] disabled:text-[#96BEE6]/70 text-white rounded-xl text-sm font-medium min-h-[44px]"
            >
              {{ isExtractingUrl ? 'Extracting...' : 'Extract' }}
            </button>
          </div>
          <p v-if="urlError" class="text-xs text-red-400">{{ urlError }}</p>
        </div>

        <div class="flex items-center gap-3 text-[#4a7aa5]/60 text-xs">
          <div class="flex-1 border-t border-[#0a2a52]"></div>
          <span>or add manually</span>
          <div class="flex-1 border-t border-[#0a2a52]"></div>
        </div>

        <input
          v-model="newName"
          placeholder="Name (required)"
          class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
        />

        <select
          v-model="newType"
          class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-[#1e407c] appearance-none"
        >
          <option v-for="opt in typeOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </option>
        </select>

        <input
          v-model="newBrand"
          placeholder="Brand (optional)"
          class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
        />

        <input
          v-model="newNotes"
          placeholder="Notes (optional)"
          class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-white text-sm placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
        />

        <div class="flex gap-2">
          <button
            @click="addWishlistItem"
            :disabled="isAdding || !newName.trim()"
            class="flex-1 bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#1e407c] disabled:text-[#96BEE6]/70 text-white py-2.5 rounded-xl text-sm font-medium"
          >
            {{ isAdding ? 'Adding...' : 'Add' }}
          </button>
          <button
            @click="showAddForm = false"
            class="px-4 py-2.5 bg-[#0a2a52] text-[#96BEE6] rounded-xl text-sm hover:bg-[#1e407c]"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Loading state -->
    <div v-if="isLoadingList && !virtualItems.length" class="text-[#96BEE6]/70 text-center py-12">
      Loading...
    </div>

    <!-- Empty states -->
    <div v-else-if="!virtualItems.length" class="text-[#96BEE6]/70 text-center py-12">
      <p v-if="normalizedSearch">No results for "{{ searchQuery.trim() }}".</p>
      <p v-else-if="activeTab === 'wishlist'">No wishlist items yet. Add something you want to try.</p>
      <p v-else>No items yet. Capture something first!</p>
    </div>

    <!-- Item list (virtual scroll) -->
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
          <!-- Collection item -->
          <router-link
            v-if="activeTab === 'collection'"
            :to="`/items/${virtualItems[row.index].item!.id}`"
            class="block bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 hover:border-[#1e407c]/50 transition-colors"
          >
            <div class="flex items-start gap-3">
              <img
                v-if="virtualItems[row.index].item!.photoUrls.length"
                :src="virtualItems[row.index].item!.photoUrls[0]"
                class="w-16 h-16 object-cover rounded-lg shrink-0"
                loading="lazy"
              />
              <div v-else class="w-16 h-16 bg-[#0a2a52] rounded-lg shrink-0 flex items-center justify-center text-xs text-[#96BEE6]/70 uppercase">
                {{ virtualItems[row.index].item!.type }}
              </div>

              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2 mb-1">
                  <span class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6]">{{ virtualItems[row.index].item!.type }}</span>
                  <span v-if="virtualItems[row.index].item!.status === 'ai-draft'" class="text-xs text-[#96BEE6]">AI Draft</span>
                </div>
                <h3 class="font-medium text-white truncate">{{ virtualItems[row.index].item!.name }}</h3>
                <p v-if="virtualItems[row.index].item!.brand" class="text-sm text-[#96BEE6]/70 truncate">{{ virtualItems[row.index].item!.brand }}</p>
                <div v-if="virtualItems[row.index].item!.userRating" class="mt-1">
                  <StarRating :rating="virtualItems[row.index].item!.userRating!" size="sm" />
                </div>
              </div>
            </div>
          </router-link>

          <!-- Wishlist item -->
          <div
            v-else-if="activeTab === 'wishlist'"
            class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"
          >
            <router-link :to="`/items/${virtualItems[row.index].item!.id}`" class="block">
              <div class="flex items-start gap-3">
                <div class="w-10 h-10 bg-[#0a2a52] rounded-lg shrink-0 flex items-center justify-center text-xs text-[#96BEE6]/70 uppercase">
                  {{ virtualItems[row.index].item!.type.slice(0, 1) }}
                </div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2 mb-1">
                    <span class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6]">{{ virtualItems[row.index].item!.type }}</span>
                  </div>
                  <h3 class="font-medium text-white truncate">{{ virtualItems[row.index].item!.name }}</h3>
                  <p v-if="virtualItems[row.index].item!.brand" class="text-sm text-[#96BEE6] truncate">{{ virtualItems[row.index].item!.brand }}</p>
                  <p v-if="virtualItems[row.index].item!.userNotes" class="text-xs text-[#96BEE6]/70 mt-1 line-clamp-2">{{ virtualItems[row.index].item!.userNotes }}</p>
                  <p v-if="virtualItems[row.index].item!.processedBy === 'pending'" class="text-xs text-[#96BEE6] mt-1">Processing...</p>
                </div>
              </div>
            </router-link>

            <div class="flex gap-2 mt-3">
              <button
                @click="convertItem(virtualItems[row.index].item!.id)"
                class="flex-1 bg-[#1e407c] hover:bg-[#2a5299] text-white py-2 min-h-[44px] rounded-xl text-sm font-medium transition-colors"
              >
                Add to Collection
              </button>
              <button
                @click="deleteItem(virtualItems[row.index].item!.id)"
                class="px-4 py-2 min-h-[44px] bg-[#0a2a52] text-red-400 hover:bg-[#1e407c] rounded-xl text-sm transition-colors"
              >
                Remove
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
  </template>
</template>

<style scoped>
.virtual-list-container {
  height: calc(100vh - 280px);
  height: calc(100dvh - 280px);
}
</style>
