<script setup lang="ts">
import { computed } from 'vue'
import StarRating from '../common/StarRating.vue'
import type { Item } from '../../services/items'

const props = defineProps<{
  items: Item[]
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'navigate', id: string): void
}>()

const maxItems = 4

const visibleItems = computed(() => props.items.slice(0, maxItems))

const fields = [
  { key: 'type', label: 'Type' },
  { key: 'brand', label: 'Brand' },
  { key: 'rating', label: 'Rating' },
  { key: 'venue', label: 'Venue' },
  { key: 'notes', label: 'Notes' },
] as const

function getField(item: Item, key: string): string {
  switch (key) {
    case 'type': return item.type || '-'
    case 'brand': return item.brand || '-'
    case 'rating': return item.userRating != null ? `${item.userRating}` : '-'
    case 'venue': return item.venue?.name || '-'
    case 'notes': return item.userNotes || '-'
    default: return '-'
  }
}
</script>

<template>
  <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-4">
    <div class="flex items-center justify-between">
      <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">
        Compare ({{ items.length }} item{{ items.length !== 1 ? 's' : '' }})
      </h3>
      <button @click="emit('close')" class="text-[#4a7aa5] hover:text-white p-1">
        <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </div>

    <div v-if="items.length < 2" class="text-center text-[#96BEE6]/50 text-sm py-6">
      Select at least 2 items to compare
    </div>

    <div v-else class="overflow-x-auto">
      <table class="w-full text-sm">
        <thead>
          <tr>
            <th class="text-left text-[#4a7aa5] text-xs font-normal pb-3 pr-4 w-24"></th>
            <th
              v-for="item in visibleItems"
              :key="item.id"
              class="text-center pb-3 px-2 min-w-[140px]"
            >
              <button
                @click="emit('navigate', item.id)"
                class="block mx-auto hover:opacity-80 transition-opacity"
              >
                <img
                  v-if="item.photoUrls?.length"
                  :src="item.photoUrls[0]"
                  :alt="item.name"
                  class="w-16 h-16 object-cover rounded-lg mx-auto mb-1"
                  loading="lazy"
                />
                <div v-else class="w-16 h-16 bg-[#0a2a52] rounded-lg mx-auto mb-1 flex items-center justify-center">
                  <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 text-[#4a7aa5]/40" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
                  </svg>
                </div>
                <span class="text-xs text-white truncate block max-w-[120px]">{{ item.name }}</span>
              </button>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="field in fields" :key="field.key" class="border-t border-[#0a2a52]">
            <td class="text-[#4a7aa5] text-xs py-2.5 pr-4 align-top">{{ field.label }}</td>
            <td
              v-for="item in visibleItems"
              :key="item.id"
              class="text-center text-white/80 py-2.5 px-2 align-top"
            >
              <StarRating v-if="field.key === 'rating' && item.userRating" :rating="item.userRating" size="sm" />
              <span v-else class="text-xs capitalize">{{ getField(item, field.key) }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
