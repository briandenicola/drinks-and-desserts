<script setup lang="ts">
import { ref, inject, onMounted, onUnmounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useItemsStore } from '../stores/items'
import { useAuthStore } from '../stores/auth'
import { RefreshKey } from '../composables/refreshKey'
import { getErrorMessage } from '../services/errors'
import { useVirtualizer } from '@tanstack/vue-virtual'
import StarRating from '../components/common/StarRating.vue'
import { useBreakpoint } from '../composables/useBreakpoint'
import FilterSidebar from '../components/collection/FilterSidebar.vue'
import type { CollectionFilters } from '../components/collection/FilterSidebar.vue'
import CollectionGrid from '../components/collection/CollectionGrid.vue'
import DetailPanel from '../components/collection/DetailPanel.vue'

const { isDesktop } = useBreakpoint()

const router = useRouter()
const itemsStore = useItemsStore()
const auth = useAuthStore()
const defaultFilter = auth.user?.preferences?.collectionFilter || undefined
// Initialize store state from user preferences if not already set
if (!itemsStore.activeFilter && defaultFilter) {
  itemsStore.activeFilter = defaultFilter
}
const activeFilter = computed({
  get: () => itemsStore.activeFilter,
  set: (v) => { itemsStore.activeFilter = v }
})
const activeTab = computed({
  get: () => itemsStore.activeTab,
  set: (v) => { itemsStore.activeTab = v }
})
const activeSort = ref(auth.user?.preferences?.collectionSort || 'rating')
const registerRefresh = inject(RefreshKey)

// Wishlist add form
const showAddForm = ref(false)
const showSortMenu = ref(false)
const showFilterMenu = ref(false)
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
  { label: 'Added', value: 'createdAt' },
  { label: 'Updated', value: 'updatedAt' },
]

registerRefresh?.(async () => {
  if (activeTab.value === 'wishlist') {
    await itemsStore.loadWishlist(activeFilter.value, true)
  } else {
    await itemsStore.loadItems(activeFilter.value, true)
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

const activeFilterLabel = computed(() =>
  typeFilters.find(f => f.value === activeFilter.value)?.label ?? 'All'
)

function setFilter(value?: string) {
  activeFilter.value = value
  if (activeTab.value === 'wishlist') {
    itemsStore.loadWishlist(value, true)
  } else {
    itemsStore.loadItems(value, true)
  }
}

function switchTab(tab: 'collection' | 'wishlist') {
  activeTab.value = tab
  activeFilter.value = defaultFilter
  if (tab === 'wishlist') {
    itemsStore.loadWishlist(defaultFilter, true)
  } else {
    itemsStore.loadItems(defaultFilter, true)
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

interface ItemGroup {
  title: string
  items: typeof itemsStore.items
}

const groupedItems = computed<ItemGroup[]>(() => {
  const source = activeTab.value === 'wishlist' ? itemsStore.wishlistItems : itemsStore.items

  // Only group when filter is "All" (undefined) in collection view
  if (activeFilter.value !== undefined || activeTab.value === 'wishlist') {
    return []
  }

  const groups: ItemGroup[] = []
  const typeMap = new Map<string, typeof source>()

  source.forEach(item => {
    const type = item.type.toLowerCase()
    if (!typeMap.has(type)) {
      typeMap.set(type, [])
    }
    typeMap.get(type)!.push(item)
  })

  // Sort each type group by rating (descending), then by updatedAt
  typeMap.forEach((typeItems) => {
    typeItems.sort((a, b) => {
      const ra = a.userRating ?? 0
      const rb = b.userRating ?? 0
      if (rb !== ra) return rb - ra
      return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
    })
  })

  // Create groups in alphabetical order by type
  const sortedTypes = Array.from(typeMap.keys()).sort()
  sortedTypes.forEach(type => {
    if (typeMap.get(type)!.length > 0) {
      const typeName = type.charAt(0).toUpperCase() + type.slice(1).replace(/-/g, ' ')
      groups.push({
        title: typeName,
        items: typeMap.get(type)!
      })
    }
  })

  return groups
})

const displayItems = computed(() => {
  const source = activeTab.value === 'wishlist' ? itemsStore.wishlistItems : itemsStore.items
  const sorted = [...source]
  const sort = activeSort.value

  sorted.sort((a, b) => {
    if (sort === 'rating') {
      const ra = a.userRating ?? 0
      const rb = b.userRating ?? 0
      if (rb !== ra) return rb - ra
      return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
    }
    if (sort === 'updatedAt') {
      return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
    }
    // createdAt (default)
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  })

  return sorted
})

const scrollContainerRef = ref<HTMLElement | null>(null)

// Flatten grouped items with headers for virtual scrolling
interface VirtualItem {
  type: 'header' | 'item'
  groupTitle?: string
  item?: typeof itemsStore.items[0]
}

const virtualItems = computed<VirtualItem[]>(() => {
  // Use grouped items when filter is "All" and in collection view
  if (groupedItems.value.length > 0) {
    const items: VirtualItem[] = []
    groupedItems.value.forEach(group => {
      items.push({ type: 'header', groupTitle: group.title })
      group.items.forEach(item => {
        items.push({ type: 'item', item })
      })
    })
    return items
  }

  // Otherwise, use flat list without headers
  return displayItems.value.map(item => ({ type: 'item' as const, item }))
})

const virtualizer = useVirtualizer(computed(() => ({
  count: virtualItems.value.length,
  getScrollElement: () => scrollContainerRef.value,
  estimateSize: (index) => {
    const item = virtualItems.value[index]
    if (item.type === 'header') return 40
    return activeTab.value === 'wishlist' ? 140 : 100
  },
  overscan: 5,
  gap: 12,
})))

watch([activeTab, activeFilter], () => {
  virtualizer.value.scrollToOffset(0)
})

const isLoadingList= computed(() =>
  activeTab.value === 'wishlist' ? itemsStore.isLoadingWishlist : itemsStore.isLoading
)

function closeSortMenu(e: MouseEvent) {
  const target = e.target as HTMLElement
  if (!target.closest('.sort-dropdown')) {
    showSortMenu.value = false
  }
  if (!target.closest('.filter-dropdown')) {
    showFilterMenu.value = false
  }
}

onMounted(() => {
  if (activeTab.value === 'wishlist') {
    itemsStore.loadWishlist(activeFilter.value, true)
  } else {
    itemsStore.loadItems(activeFilter.value, true)
  }
  document.addEventListener('click', closeSortMenu)
})

onUnmounted(() => {
  document.removeEventListener('click', closeSortMenu)
})

// Desktop collection state
const selectedItemId = ref<string | null>(null)
const desktopFilters = ref<CollectionFilters>({ category: undefined, minRating: 0, labels: '' })

const desktopFilteredItems = computed(() => {
  const f = desktopFilters.value
  return displayItems.value.filter(item => {
    if (f.category && item.type !== f.category) return false
    if (f.minRating > 0 && (item.userRating ?? 0) < f.minRating) return false
    if (f.labels.trim()) {
      const search = f.labels.toLowerCase()
      if (!item.tags.some(t => t.toLowerCase().includes(search))) return false
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
    <!-- Collection / Wishlist toggle -->
    <div class="flex bg-[#041e3e] border border-[#0a2a52] rounded-xl p-1 mb-4">
      <button
        @click="switchTab('collection')"
        class="flex-1 py-2 min-h-[44px] rounded-lg text-sm font-medium transition-colors"
        :class="activeTab === 'collection' ? 'bg-[#1e407c] text-white' : 'text-[#96BEE6] hover:text-white'"
      >
        Collection
      </button>
      <button
        @click="switchTab('wishlist')"
        class="flex-1 py-2 min-h-[44px] rounded-lg text-sm font-medium transition-colors"
        :class="activeTab === 'wishlist' ? 'bg-[#1e407c] text-white' : 'text-[#96BEE6] hover:text-white'"
      >
        Wishlist
      </button>
    </div>

    <!-- Filters + Sort -->
    <div class="flex items-center gap-2 mb-4">
      <!-- Filter dropdown -->
      <div class="relative filter-dropdown">
        <button
          @click="showFilterMenu = !showFilterMenu"
          class="flex items-center gap-1 px-3 py-2.5 min-h-[44px] rounded-full text-xs border transition-colors"
          :class="activeFilter
            ? 'bg-[#1e407c] border-[#1e407c] text-white'
            : 'border-[#1e407c]/50 text-[#96BEE6] hover:border-[#1e407c]'"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z" />
          </svg>
          <span>{{ activeFilterLabel }}</span>
        </button>

        <div
          v-if="showFilterMenu"
          class="absolute left-0 top-full mt-1 bg-[#041e3e] border border-[#1e407c]/50 rounded-xl overflow-hidden shadow-lg z-10 min-w-[140px]"
        >
          <button
            v-for="opt in typeFilters"
            :key="opt.label"
            @click="setFilter(opt.value); showFilterMenu = false"
            class="w-full text-left px-4 py-2.5 text-sm transition-colors"
            :class="activeFilter === opt.value
              ? 'text-[#96BEE6] bg-[#0a2a52]'
              : 'text-[#96BEE6] hover:bg-[#0a2a52]'"
          >
            {{ opt.label }}
          </button>
        </div>
      </div>

      <div class="flex-1"></div>

      <!-- Sort dropdown -->
      <div class="relative shrink-0 sort-dropdown">
        <button
          @click="showSortMenu = !showSortMenu"
          class="flex items-center gap-1 px-3 py-2.5 min-h-[44px] rounded-full text-xs border border-[#1e407c]/50 text-[#96BEE6] hover:border-[#1e407c] transition-colors"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3 4h13M3 8h9m-9 4h6m4 0l4-4m0 0l4 4m-4-4v12" />
          </svg>
          <span>{{ sortOptions.find(o => o.value === activeSort)?.label }}</span>
        </button>

        <div
          v-if="showSortMenu"
          class="absolute right-0 top-full mt-1 bg-[#041e3e] border border-[#1e407c]/50 rounded-xl overflow-hidden shadow-lg z-10 min-w-[140px]"
        >
          <button
            v-for="opt in sortOptions"
            :key="opt.value"
            @click="activeSort = opt.value; showSortMenu = false"
            class="w-full text-left px-4 py-2.5 text-sm transition-colors"
            :class="activeSort === opt.value
              ? 'text-[#96BEE6] bg-[#0a2a52]'
              : 'text-[#96BEE6] hover:bg-[#0a2a52]'"
          >
            {{ opt.label }}
          </button>
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
      <p v-if="activeTab === 'wishlist'">No wishlist items yet. Add something you want to try.</p>
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
          <!-- Group Header -->
          <div v-if="virtualItems[row.index].type === 'header'" class="mb-3">
            <h3 class="text-sm font-semibold text-[#96BEE6] uppercase tracking-wide">
              {{ virtualItems[row.index].groupTitle }}
            </h3>
          </div>

          <!-- Collection item -->
          <router-link
            v-else-if="activeTab === 'collection' && virtualItems[row.index].item"
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
            v-else-if="activeTab === 'wishlist' && virtualItems[row.index].item"
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
