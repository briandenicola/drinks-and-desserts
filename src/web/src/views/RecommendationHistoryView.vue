<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useBreakpoint } from '../composables/useBreakpoint'
import { recommendationsApi, type RecommendationThread } from '../services/recommendations'

const { isDesktop } = useBreakpoint()

const threads = ref<RecommendationThread[]>([])
const isLoading = ref(false)
const deletingIds = ref<Set<string>>(new Set())

async function loadThreads() {
  isLoading.value = true
  try {
    const { data } = await recommendationsApi.getThreads()
    threads.value = data
  } catch (error) {
    console.error('Failed to load recommendation history:', error)
  } finally {
    isLoading.value = false
  }
}

async function deleteThread(id: string) {
  deletingIds.value = new Set(deletingIds.value).add(id)
  try {
    await recommendationsApi.deleteThread(id)
    threads.value = threads.value.filter((t) => t.id !== id)
  } catch (error) {
    console.error('Failed to delete recommendation thread:', error)
  } finally {
    const next = new Set(deletingIds.value)
    next.delete(id)
    deletingIds.value = next
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

onMounted(loadThreads)
</script>

<template>
  <div class="p-4 mx-auto pb-24" :class="isDesktop ? 'max-w-6xl' : 'max-w-lg'">
    <div class="flex items-center gap-3 mb-6">
      <router-link to="/recommendations" class="text-[#96BEE6]/70 hover:text-[#96BEE6] transition-colors">
        <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
        </svg>
      </router-link>
      <h1 class="text-2xl font-bold text-white">Recommendation History</h1>
    </div>

    <div v-if="isLoading" class="text-center text-[#96BEE6]/70 py-8">Loading...</div>

    <div v-else-if="threads.length === 0" class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl p-4 text-sm text-white/70">
      No saved recommendations yet. Generate some recommendations and tap "Save this recommendation" to keep them here.
    </div>

    <div v-else class="space-y-3">
      <div
        v-for="thread in threads"
        :key="thread.id"
        class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl p-4 hover:border-[#96BEE6]/30 transition-colors"
      >
        <div class="flex items-start justify-between gap-3">
          <router-link :to="`/recommendations/history/${thread.id}`" class="flex-1 min-w-0">
            <p class="text-xs text-[#4a7aa5] mb-1">{{ formatDate(thread.createdAt) }}</p>
            <p class="text-white font-medium truncate">
              {{ thread.recommendations.map((r) => r.name).join(', ') }}
            </p>
            <p v-if="thread.request.preferences" class="text-sm text-white/60 mt-1 line-clamp-2">
              "{{ thread.request.preferences }}"
            </p>
            <p class="text-xs text-[#4a7aa5] mt-2">
              {{ thread.recommendations.length }} recommendation{{ thread.recommendations.length === 1 ? '' : 's' }}
              <span v-if="thread.extractedMenuItems && thread.extractedMenuItems.length > 0"> &middot; from menu photo</span>
            </p>
          </router-link>
          <button
            :disabled="deletingIds.has(thread.id)"
            @click="deleteThread(thread.id)"
            class="text-xs text-red-300/70 hover:text-red-300 px-2 py-1 disabled:opacity-50"
          >
            Delete
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
