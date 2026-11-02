<script setup lang="ts">
import type { Item } from '../../services/items'
import StarRating from '../common/StarRating.vue'

const props = defineProps<{
  items: Item[]
  selectedId: string | null
  isLoading: boolean
  hasMore: boolean
}>()

const emit = defineEmits<{
  (e: 'select', id: string): void
  (e: 'load-more'): void
}>()

function onScroll(e: Event) {
  if (!props.hasMore || props.isLoading) return

  const el = e.currentTarget as HTMLElement
  if (el.scrollHeight - el.scrollTop - el.clientHeight < 400) {
    emit('load-more')
  }
}
</script>

<template>
  <div class="flex-1 overflow-y-auto p-6" @scroll="onScroll">
    <div
      v-if="!items.length"
      class="text-th-accent/70 text-center py-20"
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
        class="text-left bg-th-inset border rounded-xl p-4 transition-colors hover:border-th-edge/80 focus:outline-none focus:ring-1 focus:ring-th-emphasis/40"
        :class="selectedId === item.id ? 'border-th-emphasis' : 'border-th-line'"
      >
        <img
          v-if="item.photoUrls.length"
          :src="item.photoUrls[0]"
          class="w-full h-36 object-cover rounded-lg mb-3"
          loading="lazy"
        />
        <div
          v-else
          class="w-full h-36 bg-th-card rounded-lg mb-3 flex items-center justify-center text-sm text-th-accent/70 uppercase"
        >
          {{ item.type }}
        </div>

        <span class="inline-block text-xs px-2 py-0.5 rounded-full bg-th-card text-th-accent mb-1">
          {{ item.type }}
        </span>

        <h3 class="font-medium text-th-heading truncate text-sm">{{ item.name }}</h3>
        <p v-if="item.brand" class="text-xs text-th-accent/70 truncate">{{ item.brand }}</p>

        <div v-if="item.userRating" class="mt-2">
          <StarRating :rating="item.userRating" size="sm" />
        </div>
      </button>
    </div>

    <div v-if="isLoading && items.length" class="text-th-accent/70 text-center py-6 text-sm">
      Loading more...
    </div>
  </div>
</template>
