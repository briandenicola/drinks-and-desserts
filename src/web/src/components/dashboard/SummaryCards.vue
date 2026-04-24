<script setup lang="ts">
defineProps<{
  summary: {
    totalItems: number
    drinkCount: number
    dessertCount: number
    avgDrinkRating: number
    avgDessertRating: number
    wishlistSize: number
    totalVenues: number
  }
}>()

const cards = [
  { key: 'totalItems', label: 'Collection' },
  { key: 'drinkCount', label: 'Drinks' },
  { key: 'dessertCount', label: 'Desserts' },
  { key: 'wishlistSize', label: 'Wishlist' },
  { key: 'totalVenues', label: 'Venues' },
] as const
</script>

<template>
  <div class="grid grid-cols-2 lg:grid-cols-5 gap-3">
    <div
      v-for="card in cards"
      :key="card.key"
      class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 text-center"
    >
      <div class="text-2xl font-bold text-[#96BEE6]">{{ summary[card.key] }}</div>
      <div class="text-xs text-[#96BEE6]/70 mt-1">{{ card.label }}</div>
    </div>

    <!-- Rating cards -->
    <div v-if="summary.avgDrinkRating > 0" class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 text-center">
      <div class="text-2xl font-bold text-[#96BEE6]">{{ summary.avgDrinkRating }}</div>
      <div class="text-xs text-[#96BEE6]/70 mt-1">Avg Drink Rating</div>
    </div>
    <div v-if="summary.avgDessertRating > 0" class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 text-center">
      <div class="text-2xl font-bold text-[#96BEE6]">{{ summary.avgDessertRating }}</div>
      <div class="text-xs text-[#96BEE6]/70 mt-1">Avg Dessert Rating</div>
    </div>
  </div>
</template>
