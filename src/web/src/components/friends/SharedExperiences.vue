<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  friends: {
    friendId: string
    friendDisplayName: string
    status: string
  }[]
  userItems: { id: string; name: string; type: string; venue?: { name: string } | null }[]
  friendItems: { id: string; name: string; type: string; venue?: { name: string } | null }[]
}>()

const sharedVenues = computed(() => {
  const userVenues = new Set(
    props.userItems
      .filter(i => i.venue?.name)
      .map(i => i.venue!.name.toLowerCase())
  )
  const friendVenueNames = props.friendItems
    .filter(i => i.venue?.name && userVenues.has(i.venue.name.toLowerCase()))
    .map(i => i.venue!.name)
  return [...new Set(friendVenueNames)]
})

const sharedTypes = computed(() => {
  const userTypes = new Set(props.userItems.map(i => i.type))
  return [...userTypes].filter(t => props.friendItems.some(fi => fi.type === t))
})
</script>

<template>
  <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
    <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Shared Experiences</h3>

    <div v-if="!sharedVenues.length && !sharedTypes.length" class="text-center py-6 text-[#96BEE6]/50 text-sm">
      No shared experiences found yet
    </div>

    <div v-if="sharedVenues.length" class="space-y-2">
      <h4 class="text-xs text-[#96BEE6]/70">Shared Venues</h4>
      <div v-for="venue in sharedVenues" :key="venue" class="flex items-center gap-2 text-sm text-white/80">
        <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4 text-[#96BEE6]/50 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
          <path stroke-linecap="round" stroke-linejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
        </svg>
        <span>{{ venue }}</span>
      </div>
    </div>

    <div v-if="sharedTypes.length" class="space-y-2">
      <h4 class="text-xs text-[#96BEE6]/70">Shared Categories</h4>
      <div class="flex flex-wrap gap-2">
        <span
          v-for="t in sharedTypes"
          :key="t"
          class="text-xs px-2 py-1 rounded-full bg-[#1e407c]/30 text-[#96BEE6]/80 capitalize"
        >{{ t }}</span>
      </div>
    </div>
  </section>
</template>
