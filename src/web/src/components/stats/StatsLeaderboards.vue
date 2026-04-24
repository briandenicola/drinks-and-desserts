<script setup lang="ts">
import { useRouter } from 'vue-router'
import StarRating from '../common/StarRating.vue'

const router = useRouter()

defineProps<{
  topRated: { id: string; name: string; type: string; brand: string | null; rating: number; photoUrl: string | null }[]
  topVenues: { name: string; count: number }[]
}>()
</script>

<template>
  <div class="grid lg:grid-cols-2 gap-6">
    <!-- Top Rated Items -->
    <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
      <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Top Rated</h3>
      <div v-if="!topRated.length" class="text-center py-6 text-[#96BEE6]/50 text-sm">No rated items yet</div>
      <div
        v-for="(item, idx) in topRated"
        :key="item.id"
        @click="router.push(`/items/${item.id}`)"
        class="flex items-center gap-3 cursor-pointer hover:bg-[#0a2a52]/50 rounded-lg p-2 -mx-2 transition-colors"
      >
        <span class="text-lg font-bold text-[#4a7aa5]/60 w-6 text-center">{{ idx + 1 }}</span>
        <img
          v-if="item.photoUrl"
          :src="item.photoUrl"
          class="w-10 h-10 rounded-lg object-cover"
          alt=""
        />
        <div v-else class="w-10 h-10 rounded-lg bg-[#0a2a52] flex items-center justify-center">
          <span class="text-xs text-[#4a7aa5]/60">N/A</span>
        </div>
        <div class="flex-1 min-w-0">
          <div class="text-sm text-white truncate">{{ item.name }}</div>
          <div class="text-xs text-[#96BEE6]/70 capitalize">{{ item.brand || item.type }}</div>
        </div>
        <StarRating :rating="item.rating" size="sm" />
      </div>
    </section>

    <!-- Favorite Venues -->
    <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
      <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Top Venues</h3>
      <div v-if="!topVenues.length" class="text-center py-6 text-[#96BEE6]/50 text-sm">No venue data yet</div>
      <div v-for="(v, idx) in topVenues" :key="v.name" class="flex items-center gap-3 p-2 -mx-2">
        <span class="text-lg font-bold text-[#4a7aa5]/60 w-6 text-center">{{ idx + 1 }}</span>
        <span class="text-sm text-white/80 truncate flex-1">{{ v.name }}</span>
        <span class="text-xs text-[#96BEE6]/70 shrink-0">{{ v.count }} item{{ v.count !== 1 ? 's' : '' }}</span>
      </div>
    </section>
  </div>
</template>
