<script setup lang="ts">
import type { Item } from '../../services/items'
import StarRating from '../common/StarRating.vue'

defineProps<{
  items: Item[]
  selectedId: string | null
}>()

const emit = defineEmits<{
  (e: 'select', id: string): void
}>()
</script>

<template>
  <div class="flex-1 overflow-y-auto p-6">
    <div
      v-if="!items.length"
      class="text-[#96BEE6]/70 text-center py-20"
    >
      <p>No items match the current filters.</p>
    </div>

    <div
      v-else
      class="grid grid-cols-3 xl:grid-cols-4 gap-4"
    >
      <button
        v-for="item in items"
        :key="item.id"
        @click="emit('select', item.id)"
        class="text-left bg-[#041e3e] border rounded-xl p-4 transition-colors hover:border-[#1e407c]/80 focus:outline-none focus:ring-1 focus:ring-[#96BEE6]/40"
        :class="selectedId === item.id ? 'border-[#96BEE6]' : 'border-[#0a2a52]'"
      >
        <img
          v-if="item.photoUrls.length"
          :src="item.photoUrls[0]"
          class="w-full h-36 object-cover rounded-lg mb-3"
          loading="lazy"
        />
        <div
          v-else
          class="w-full h-36 bg-[#0a2a52] rounded-lg mb-3 flex items-center justify-center text-sm text-[#96BEE6]/70 uppercase"
        >
          {{ item.type }}
        </div>

        <span class="inline-block text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6] mb-1">
          {{ item.type }}
        </span>

        <h3 class="font-medium text-white truncate text-sm">{{ item.name }}</h3>
        <p v-if="item.brand" class="text-xs text-[#96BEE6]/70 truncate">{{ item.brand }}</p>

        <div v-if="item.userRating" class="mt-2">
          <StarRating :rating="item.userRating" size="sm" />
        </div>
      </button>
    </div>
  </div>
</template>
