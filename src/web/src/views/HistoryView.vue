<script setup lang="ts">
import { ref, computed, inject, onMounted } from 'vue'
import { useCapturesStore } from '../stores/captures'
import { venuesApi, type Venue } from '../services/venues'
import { type CaptureResponse } from '../services/captures'
import { RefreshKey } from '../composables/refreshKey'

const capturesStore = useCapturesStore()
const registerRefresh = inject(RefreshKey)

const venues = ref<Venue[]>([])
const venuesLoading = ref(false)
const activeFilter = ref<'all' | 'captures' | 'venues'>('all')

interface ActivityEntry {
  type: 'capture' | 'venue'
  id: string
  timestamp: string
  data: CaptureResponse | Venue
}

const activityFeed = computed<ActivityEntry[]>(() => {
  const entries: ActivityEntry[] = []

  if (activeFilter.value !== 'venues') {
    for (const c of capturesStore.captures) {
      entries.push({ type: 'capture', id: c.id, timestamp: c.createdAt, data: c })
    }
  }

  if (activeFilter.value !== 'captures') {
    for (const v of venues.value) {
      entries.push({ type: 'venue', id: v.id, timestamp: v.createdAt, data: v })
    }
  }

  entries.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
  return entries
})

const captureCount = computed(() => capturesStore.captures.length)
const venueCount = computed(() => venues.value.length)

registerRefresh?.(() => loadAll(true))

onMounted(() => loadAll(true))

async function loadAll(reset = false) {
  venuesLoading.value = true
  try {
    await Promise.all([
      capturesStore.loadCaptures(reset),
      venuesApi.list().then(res => { venues.value = res.data.items }),
    ])
  } finally {
    venuesLoading.value = false
  }
}

function captureStatusColor(status: string) {
  switch (status) {
    case 'completed': return 'text-green-400'
    case 'processing': return 'text-[#96BEE6]'
    case 'failed': return 'text-red-400'
    default: return 'text-[#96BEE6]'
  }
}

function captureStatusLabel(status: string) {
  switch (status) {
    case 'completed': return 'Complete'
    case 'processing': return 'Processing'
    case 'failed': return 'Failed'
    default: return 'Pending'
  }
}

function venueStatusColor(status: string) {
  switch (status) {
    case 'completed': return 'text-green-400'
    case 'processing': return 'text-[#96BEE6]'
    case 'failed': return 'text-red-400'
    default: return 'text-[#96BEE6]'
  }
}

function venueStatusLabel(status: string) {
  switch (status) {
    case 'completed': return 'Complete'
    case 'processing': return 'Processing'
    case 'failed': return 'Failed'
    default: return 'Manual'
  }
}

function workflowStepIcon(status: string) {
  switch (status) {
    case 'complete': return '●'
    case 'running': return '◎'
    case 'error': return '✕'
    default: return '○'
  }
}

function workflowStepColor(status: string) {
  switch (status) {
    case 'complete': return 'text-green-400'
    case 'running': return 'text-[#96BEE6]'
    case 'error': return 'text-red-400'
    default: return 'text-[#4a7aa5]'
  }
}

const expandedVenueId = ref<string | null>(null)
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <h2 class="text-xl font-semibold mb-4">History</h2>

    <!-- Filter tabs -->
    <div class="flex gap-2 mb-4">
      <button
        v-for="filter in (['all', 'captures', 'venues'] as const)"
        :key="filter"
        @click="activeFilter = filter"
        class="flex-1 py-2 min-h-[44px] rounded-xl text-sm font-medium transition-colors"
        :class="activeFilter === filter
          ? 'bg-[#1e407c]/30 text-[#96BEE6] border border-[#1e407c]'
          : 'bg-[#0a2a52] text-[#96BEE6] border border-[#1e407c]/50 hover:bg-[#0a2a52]'"
      >
        {{ filter === 'all' ? `All (${captureCount + venueCount})` :
           filter === 'captures' ? `Captures (${captureCount})` :
           `Venues (${venueCount})` }}
      </button>
    </div>

    <div v-if="(capturesStore.isLoading || venuesLoading) && !activityFeed.length" class="text-[#96BEE6]/70 text-center py-12">
      Loading...
    </div>

    <div v-else-if="!activityFeed.length" class="text-[#96BEE6]/70 text-center py-12">
      <p>No activity yet. Head to Capture to get started!</p>
    </div>

    <div v-else class="space-y-3">
      <template v-for="entry in activityFeed" :key="entry.type + '-' + entry.id">

        <!-- Capture entry -->
        <router-link
          v-if="entry.type === 'capture'"
          :to="`/history/${entry.id}`"
          class="block bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 hover:border-[#1e407c]/50 transition-colors"
        >
          <div class="flex items-start justify-between mb-2">
            <div class="flex items-center gap-2">
              <span class="text-xs px-2 py-0.5 rounded-full bg-[#1e407c]/30 text-[#96BEE6]">Capture</span>
              <span :class="captureStatusColor((entry.data as CaptureResponse).status)" class="text-sm">
                {{ captureStatusLabel((entry.data as CaptureResponse).status) }}
              </span>
            </div>
            <span class="text-xs text-[#4a7aa5]/60">
              {{ new Date(entry.timestamp).toLocaleDateString() }}
            </span>
          </div>

          <div v-if="(entry.data as CaptureResponse).photos.length" class="flex gap-1 mb-2 overflow-x-auto">
            <img
              v-for="(photo, i) in (entry.data as CaptureResponse).photos.slice(0, 4)"
              :key="i"
              :src="photo"
              class="w-12 h-12 object-cover rounded"
            />
            <span v-if="(entry.data as CaptureResponse).photos.length > 4" class="text-[#96BEE6]/70 text-xs self-center ml-1">
              +{{ (entry.data as CaptureResponse).photos.length - 4 }}
            </span>
          </div>

          <p v-if="(entry.data as CaptureResponse).userNote" class="text-sm text-[#96BEE6] line-clamp-2">
            {{ (entry.data as CaptureResponse).userNote }}
          </p>

          <div class="flex items-center justify-between mt-2">
            <span v-if="(entry.data as CaptureResponse).workflowSteps?.length" class="text-xs text-[#4a7aa5]/60">
              {{ (entry.data as CaptureResponse).workflowSteps.filter(s => s.status === 'complete').length }}/{{ (entry.data as CaptureResponse).workflowSteps.length }} workflow steps
            </span>
            <span v-if="(entry.data as CaptureResponse).itemIds.length" class="text-xs text-[#96BEE6]">
              {{ (entry.data as CaptureResponse).itemIds.length }} item(s) →
            </span>
          </div>
        </router-link>

        <!-- Venue entry -->
        <div
          v-else
          class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 hover:border-[#1e407c]/50 transition-colors"
          :class="(entry.data as Venue).workflowSteps?.length ? 'cursor-pointer' : ''"
          @click="(entry.data as Venue).workflowSteps?.length ? (expandedVenueId = expandedVenueId === entry.id ? null : entry.id) : $router.push(`/venues/${entry.id}`)"
        >
          <div class="flex items-start justify-between mb-2">
            <div class="flex items-center gap-2">
              <span class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6]">Venue</span>
              <span :class="venueStatusColor((entry.data as Venue).status)" class="text-sm">
                {{ venueStatusLabel((entry.data as Venue).status) }}
              </span>
            </div>
            <span class="text-xs text-[#4a7aa5]/60">
              {{ new Date(entry.timestamp).toLocaleDateString() }}
            </span>
          </div>

          <p class="text-sm text-white font-medium">{{ (entry.data as Venue).name }}</p>
          <p v-if="(entry.data as Venue).address" class="text-xs text-[#96BEE6]/70 mt-0.5">
            {{ (entry.data as Venue).address }}
          </p>

          <div v-if="(entry.data as Venue).labels?.length" class="flex flex-wrap gap-1 mt-2">
            <span
              v-for="label in (entry.data as Venue).labels.slice(0, 3)"
              :key="label"
              class="text-xs px-2 py-0.5 rounded-full bg-[#1e407c]/30 text-[#96BEE6]"
            >
              {{ label }}
            </span>
            <span v-if="(entry.data as Venue).labels.length > 3" class="text-xs text-[#4a7aa5]/60 self-center">
              +{{ (entry.data as Venue).labels.length - 3 }}
            </span>
          </div>

          <!-- Workflow steps (expandable) -->
          <div v-if="(entry.data as Venue).workflowSteps?.length">
            <div v-if="expandedVenueId === entry.id" class="mt-3 border-t border-[#0a2a52] pt-3">
              <p class="text-xs text-[#96BEE6]/70 mb-2 font-medium">Agent Workflow</p>
              <div class="space-y-2">
                <div
                  v-for="step in (entry.data as Venue).workflowSteps"
                  :key="step.stepId"
                  class="flex items-start gap-2"
                >
                  <span :class="workflowStepColor(step.status)" class="text-sm mt-0.5">{{ workflowStepIcon(step.status) }}</span>
                  <div class="min-w-0 flex-1">
                    <p class="text-xs text-white">{{ step.agentName }}</p>
                    <p v-if="step.summary" class="text-xs text-[#96BEE6]/70 line-clamp-2">{{ step.summary }}</p>
                    <p v-if="step.detail" class="text-xs text-[#4a7aa5]/60 line-clamp-1">{{ step.detail }}</p>
                    <p class="text-xs text-[#4a7aa5]/60">
                      {{ new Date(step.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }) }}
                    </p>
                  </div>
                </div>
              </div>

              <div v-if="(entry.data as Venue).processingError" class="mt-2 p-2 bg-red-900/20 border border-red-800/30 rounded-lg">
                <p class="text-xs text-red-400">{{ (entry.data as Venue).processingError }}</p>
              </div>

              <router-link :to="`/venues/${entry.id}`" class="block text-xs text-[#96BEE6] mt-2 hover:text-white">
                View venue details →
              </router-link>
            </div>

            <p v-else class="text-xs text-[#4a7aa5]/60 mt-1">
              {{ (entry.data as Venue).workflowSteps.filter(s => s.status === 'complete').length }}/{{ (entry.data as Venue).workflowSteps.length }} workflow steps — tap to expand
            </p>
          </div>
        </div>

      </template>
    </div>
  </div>
</template>
