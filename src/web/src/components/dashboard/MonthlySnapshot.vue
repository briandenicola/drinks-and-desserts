<script setup lang="ts">
defineProps<{
  snapshot: {
    newItemsCaptured: number
    venuesVisited: number
    wishlistConversions: number
    month: string
  }
}>()

function formatMonth(monthStr: string) {
  const [year, month] = monthStr.split('-')
  const date = new Date(Number(year), Number(month) - 1)
  return date.toLocaleDateString('en-US', { month: 'long', year: 'numeric' })
}
</script>

<template>
  <section class="bg-th-inset border border-th-line rounded-xl p-4 space-y-3">
    <h3 class="text-sm font-medium text-th-accent uppercase tracking-wide">
      This Month
      <span class="text-th-muted/60 normal-case font-normal ml-1">({{ formatMonth(snapshot.month) }})</span>
    </h3>

    <div class="grid grid-cols-3 gap-3">
      <div class="text-center">
        <div class="text-xl font-bold text-th-accent">{{ snapshot.newItemsCaptured }}</div>
        <div class="text-[10px] text-th-accent/70">New Items</div>
      </div>
      <div class="text-center">
        <div class="text-xl font-bold text-th-accent">{{ snapshot.venuesVisited }}</div>
        <div class="text-[10px] text-th-accent/70">Venues Visited</div>
      </div>
      <div class="text-center">
        <div class="text-xl font-bold text-th-accent">{{ snapshot.wishlistConversions }}</div>
        <div class="text-[10px] text-th-accent/70">Wishlist Converted</div>
      </div>
    </div>
  </section>
</template>
