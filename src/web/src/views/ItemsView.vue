<script setup lang="ts">
import { ref, inject, onMounted } from 'vue'
import { useItemsStore } from '../stores/items'
import { RefreshKey } from '../composables/refreshKey'

const itemsStore = useItemsStore()
const activeFilter = ref<string | undefined>()
const registerRefresh = inject(RefreshKey)

registerRefresh?.(() => itemsStore.loadItems(activeFilter.value, true))

const typeFilters = [
  { label: 'All', value: undefined },
  { label: 'Whiskey', value: 'whiskey' },
  { label: 'Wine', value: 'wine' },
  { label: 'Cocktail', value: 'cocktail' },
  { label: 'Cigar', value: 'cigar' },
]

function setFilter(value?: string) {
  activeFilter.value = value
  itemsStore.loadItems(value, true)
}

onMounted(() => {
  itemsStore.loadItems(undefined, true)
})
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <h2 class="text-xl font-semibold mb-4">My Collection</h2>

    <!-- Type filters -->
    <div class="flex gap-2 mb-4 overflow-x-auto pb-2">
      <button
        v-for="filter in typeFilters"
        :key="filter.label"
        @click="setFilter(filter.value)"
        class="shrink-0 px-3 py-1.5 rounded-full text-sm border transition-colors"
        :class="activeFilter === filter.value
          ? 'bg-amber-700 border-amber-600 text-white'
          : 'bg-stone-900 border-stone-700 text-stone-400 hover:border-stone-600'"
      >
        {{ filter.label }}
      </button>
    </div>

    <div v-if="itemsStore.isLoading && !itemsStore.items.length" class="text-stone-500 text-center py-12">
      Loading...
    </div>

    <div v-else-if="!itemsStore.items.length" class="text-stone-500 text-center py-12">
      <p>No items yet. Capture something first!</p>
    </div>

    <div v-else class="space-y-3">
      <router-link
        v-for="item in itemsStore.items"
        :key="item.id"
        :to="`/items/${item.id}`"
        class="block bg-stone-900 border border-stone-800 rounded-xl p-4 hover:border-stone-700 transition-colors"
      >
        <div class="flex items-start gap-3">
          <img
            v-if="item.photoUrls.length"
            :src="item.photoUrls[0]"
            class="w-16 h-16 object-cover rounded-lg shrink-0"
          />
          <div v-else class="w-16 h-16 bg-stone-800 rounded-lg shrink-0 flex items-center justify-center text-xs text-stone-500 uppercase">
            {{ item.type }}
          </div>

          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2 mb-1">
              <span class="text-xs px-2 py-0.5 rounded-full bg-stone-800 text-stone-400">{{ item.type }}</span>
              <span v-if="item.status === 'ai-draft'" class="text-xs text-amber-500">AI Draft</span>
            </div>
            <h3 class="font-medium text-stone-100 truncate">{{ item.name }}</h3>
            <p v-if="item.brand" class="text-sm text-stone-500 truncate">{{ item.brand }}</p>
            <div v-if="item.userRating" class="mt-1 text-amber-500 text-sm">
              {{ '★'.repeat(item.userRating) }}{{ '☆'.repeat(5 - item.userRating) }}
            </div>
          </div>
        </div>
      </router-link>
    </div>
  </div>
</template>
