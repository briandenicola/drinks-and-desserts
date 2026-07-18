<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useBreakpoint } from '../composables/useBreakpoint'
import { recommendationsApi, type RecommendationThread } from '../services/recommendations'

const route = useRoute()
const { isDesktop } = useBreakpoint()

const thread = ref<RecommendationThread | null>(null)
const isLoading = ref(false)
const notFound = ref(false)

async function loadThread() {
  isLoading.value = true
  notFound.value = false
  try {
    const { data } = await recommendationsApi.getThread(route.params.id as string)
    thread.value = data
  } catch (error) {
    console.error('Failed to load recommendation thread:', error)
    notFound.value = true
  } finally {
    isLoading.value = false
  }
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
}

onMounted(loadThread)
</script>

<template>
  <div class="p-4 mx-auto pb-24" :class="isDesktop ? 'max-w-6xl' : 'max-w-lg'">
    <div class="flex items-center gap-3 mb-6">
      <router-link to="/recommendations/history" class="text-[#96BEE6]/70 hover:text-[#96BEE6] transition-colors">
        <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
        </svg>
      </router-link>
      <h1 class="text-2xl font-bold text-white">Saved Recommendation</h1>
    </div>

    <div v-if="isLoading" class="text-center text-[#96BEE6]/70 py-8">Loading...</div>

    <div v-else-if="notFound" class="bg-red-900/30 border border-red-700/50 text-red-300 rounded-xl p-4 text-sm">
      This recommendation thread could not be found.
    </div>

    <div v-else-if="thread" class="space-y-4">
      <div class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl p-4">
        <p class="text-xs text-[#4a7aa5] mb-3">{{ formatDate(thread.createdAt) }}</p>

        <div v-if="thread.request.preferences" class="mb-3">
          <p class="text-[#96BEE6] text-sm font-medium mb-1">Preferences</p>
          <p class="text-sm text-white/70">{{ thread.request.preferences }}</p>
        </div>

        <div v-if="thread.request.itemTypes && thread.request.itemTypes.length > 0" class="mb-3">
          <p class="text-[#96BEE6] text-sm font-medium mb-1">Focused on</p>
          <div class="flex flex-wrap gap-2">
            <span
              v-for="type in thread.request.itemTypes"
              :key="type"
              class="px-2 py-1 rounded-full text-xs bg-[#1e407c] text-white"
            >
              {{ type }}
            </span>
          </div>
        </div>

        <p v-if="thread.reasoning" class="text-sm text-white/70 mb-3">{{ thread.reasoning }}</p>

        <div v-if="thread.basedOnItems.length > 0" class="text-xs text-[#4a7aa5]">
          Based on: {{ thread.basedOnItems.join(', ') }}
        </div>
      </div>

      <div v-if="thread.extractedMenuItems && thread.extractedMenuItems.length > 0" class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl p-4">
        <h3 class="text-[#96BEE6] text-sm font-medium mb-2">Menu Items Found</h3>
        <ul class="space-y-1">
          <li v-for="(item, index) in thread.extractedMenuItems" :key="index" class="text-sm text-white/80">
            • {{ item }}
          </li>
        </ul>
      </div>

      <div
        v-for="(rec, index) in thread.recommendations"
        :key="index"
        class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl p-4"
      >
        <div class="flex items-start justify-between mb-2">
          <div class="flex-1">
            <h3 class="text-white font-medium">{{ rec.name }}</h3>
            <p class="text-sm text-[#96BEE6]">{{ rec.type }}</p>
            <p v-if="rec.brand" class="text-sm text-[#4a7aa5]">{{ rec.brand }}</p>
            <p v-if="rec.category" class="text-sm text-[#4a7aa5]">{{ rec.category }}</p>
          </div>
          <div class="flex flex-col items-end gap-1">
            <span class="text-xs bg-[#1e407c] text-white px-2 py-1 rounded">
              {{ (rec.confidence * 100).toFixed(0) }}% match
            </span>
            <span v-if="rec.matchedFromMenu" class="text-xs bg-green-900/50 text-green-300 px-2 py-1 rounded">
              On menu
            </span>
          </div>
        </div>
        <p class="text-sm text-white/70 mt-2">{{ rec.reason }}</p>
      </div>
    </div>
  </div>
</template>
