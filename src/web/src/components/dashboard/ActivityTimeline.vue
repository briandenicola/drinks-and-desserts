<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

defineProps<{
  activities: {
    captureId: string
    status: 'processing' | 'complete' | 'failed'
    thumbnailUrls: string[]
    itemCount: number
    createdAt: string
    venueName: string | null
  }[]
}>()

function statusBadgeClass(status: string) {
  switch (status) {
    case 'complete': return 'bg-emerald-600/20 text-emerald-400'
    case 'failed': return 'bg-red-600/20 text-red-400'
    default: return 'bg-[#1e407c]/40 text-[#96BEE6]'
  }
}

function statusLabel(status: string) {
  switch (status) {
    case 'complete': return 'Complete'
    case 'failed': return 'Failed'
    default: return 'Processing'
  }
}

function formatDate(dateStr: string) {
  const date = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffHrs = Math.floor(diffMs / 3600000)
  if (diffHrs < 1) return 'Just now'
  if (diffHrs < 24) return `${diffHrs}h ago`
  const diffDays = Math.floor(diffHrs / 24)
  if (diffDays < 7) return `${diffDays}d ago`
  return date.toLocaleDateString()
}

function onImgError(e: Event) {
  const img = e.target as HTMLImageElement
  img.style.display = 'none'
}
</script>

<template>
  <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
    <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Recent Activity</h3>

    <div v-if="!activities.length" class="text-center py-6 text-[#96BEE6]/50 text-sm">
      No captures yet. Use the mobile app to capture your first item.
    </div>

    <div
      v-for="activity in activities"
      :key="activity.captureId"
      @click="router.push(`/history/${activity.captureId}`)"
      class="flex items-center gap-3 p-2 -mx-2 rounded-lg cursor-pointer hover:bg-[#0a2a52]/50 transition-colors"
    >
      <!-- Thumbnail -->
      <div class="w-12 h-12 rounded-lg bg-[#0a2a52] overflow-hidden shrink-0 flex items-center justify-center">
        <img
          v-if="activity.thumbnailUrls.length"
          :src="activity.thumbnailUrls[0]"
          class="w-full h-full object-cover"
          alt=""
          @error="onImgError"
        />
        <svg v-else xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 text-[#4a7aa5]/60" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
        </svg>
      </div>

      <!-- Details -->
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2">
          <span
            class="text-[10px] px-1.5 py-0.5 rounded-full font-medium"
            :class="statusBadgeClass(activity.status)"
          >{{ statusLabel(activity.status) }}</span>
          <span v-if="activity.itemCount > 0" class="text-xs text-[#96BEE6]/70">
            {{ activity.itemCount }} item{{ activity.itemCount !== 1 ? 's' : '' }}
          </span>
        </div>
        <div v-if="activity.venueName" class="text-xs text-[#96BEE6]/50 truncate mt-0.5">
          {{ activity.venueName }}
        </div>
      </div>

      <!-- Time -->
      <span class="text-xs text-[#4a7aa5]/60 shrink-0">{{ formatDate(activity.createdAt) }}</span>
    </div>
  </section>
</template>
