<script setup lang="ts">
import { computed } from 'vue'
import StarRating from '../common/StarRating.vue'

const props = defineProps<{
  venues: {
    id: string
    name: string
    type: string
    rating: number | null
    itemCount?: number
  }[]
  sortBy: 'rating' | 'items'
}>()

const emit = defineEmits<{
  (e: 'sort', by: 'rating' | 'items'): void
  (e: 'select', id: string): void
}>()

const sorted = computed(() => {
  return [...props.venues].sort((a, b) => {
    if (props.sortBy === 'rating') {
      return (b.rating ?? 0) - (a.rating ?? 0)
    }
    return (b.itemCount ?? 0) - (a.itemCount ?? 0)
  })
})
</script>

<template>
  <section class="bg-th-inset border border-th-line rounded-xl p-4 space-y-3">
    <div class="flex items-center justify-between">
      <h3 class="text-sm font-medium text-th-emphasis uppercase tracking-wide">Venue Leaderboard</h3>
      <div class="flex gap-1">
        <button
          @click="emit('sort', 'rating')"
          class="text-[10px] px-2 py-1 rounded-full transition-colors"
          :class="sortBy === 'rating' ? 'bg-th-brand text-white' : 'text-th-accent/70 hover:text-th-accent'"
        >Rating</button>
        <button
          @click="emit('sort', 'items')"
          class="text-[10px] px-2 py-1 rounded-full transition-colors"
          :class="sortBy === 'items' ? 'bg-th-brand text-white' : 'text-th-accent/70 hover:text-th-accent'"
        >Items</button>
      </div>
    </div>

    <div v-if="!sorted.length" class="text-center py-6 text-th-accent/50 text-sm">No venues yet</div>

    <div
      v-for="(venue, idx) in sorted.slice(0, 10)"
      :key="venue.id"
      @click="emit('select', venue.id)"
      class="flex items-center gap-3 p-2 -mx-2 rounded-lg cursor-pointer hover:bg-th-card-hover/50 transition-colors"
    >
      <span class="text-lg font-bold text-th-muted/60 w-6 text-center">{{ idx + 1 }}</span>
      <div class="flex-1 min-w-0">
        <div class="text-sm text-th-text truncate">{{ venue.name }}</div>
        <div class="text-xs text-th-accent/70 capitalize">{{ venue.type }}</div>
      </div>
      <div class="flex items-center gap-2 shrink-0">
        <span v-if="venue.itemCount" class="text-xs text-th-accent/50">{{ venue.itemCount }} item{{ venue.itemCount !== 1 ? 's' : '' }}</span>
        <StarRating v-if="venue.rating" :rating="venue.rating" size="sm" />
      </div>
    </div>
  </section>
</template>
