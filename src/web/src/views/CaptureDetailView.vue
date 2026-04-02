<script setup lang="ts">
import { ref, computed, inject, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { capturesApi, type CaptureResponse, type WorkflowStep } from '../services/captures'
import { itemsApi, type Item } from '../services/items'
import { RefreshKey } from '../composables/refreshKey'

const route = useRoute()
const router = useRouter()
const registerRefresh = inject(RefreshKey)

const capture = ref<CaptureResponse | null>(null)
const items = ref<Item[]>([])
const isLoading = ref(true)
const isReprocessing = ref(false)
let pollInterval: ReturnType<typeof setInterval> | null = null

const fallbackAgentNames = ['Local Extraction', 'Fallback']

function isAiStep(step: WorkflowStep): boolean {
  return !fallbackAgentNames.includes(step.agentName)
}

const usedAi = computed(() => {
  if (!capture.value?.workflowSteps.length) return false
  return capture.value.workflowSteps.some(s => isAiStep(s))
})

const canReprocess = computed(() => {
  if (!capture.value) return false
  return capture.value.status === 'completed' || capture.value.status === 'failed'
})

registerRefresh?.(async () => { await loadCapture() })

onMounted(async () => {
  await loadCapture()
  if (capture.value && capture.value.status === 'processing') {
    startPolling()
  }
})

onUnmounted(() => {
  if (pollInterval) clearInterval(pollInterval)
})

async function loadCapture() {
  try {
    const { data } = await capturesApi.get(route.params.id as string)
    capture.value = data
    if (data.status === 'completed' && data.itemIds.length > 0) {
      const loaded: Item[] = []
      for (const itemId of data.itemIds) {
        try {
          const { data: item } = await itemsApi.get(itemId)
          loaded.push(item)
        } catch { /* item may not exist yet */ }
      }
      items.value = loaded
    } else {
      items.value = []
    }
  } catch {
    capture.value = null
  } finally {
    isLoading.value = false
  }
}

function startPolling() {
  if (pollInterval) clearInterval(pollInterval)
  pollInterval = setInterval(async () => {
    await loadCapture()
    if (capture.value && capture.value.status !== 'processing') {
      if (pollInterval) clearInterval(pollInterval)
      pollInterval = null
    }
  }, 3000)
}

async function reprocess() {
  if (!capture.value || isReprocessing.value) return
  isReprocessing.value = true
  try {
    const { data } = await capturesApi.reprocess(capture.value.id)
    capture.value = data
    items.value = []
    startPolling()
  } finally {
    isReprocessing.value = false
  }
}

function statusLabel(status: string): string {
  switch (status) {
    case 'complete': return 'Complete'
    case 'running': return 'Running'
    case 'error': return 'Error'
    default: return 'Pending'
  }
}

function statusColor(status: string) {
  switch (status) {
    case 'complete': return 'text-green-400 border-green-800'
    case 'running': return 'text-amber-400 border-amber-800'
    case 'error': return 'text-red-400 border-red-800'
    default: return 'text-stone-500 border-stone-800'
  }
}

function dotColor(status: string) {
  switch (status) {
    case 'complete': return 'bg-green-500'
    case 'running': return 'bg-amber-500 animate-pulse'
    case 'error': return 'bg-red-500'
    default: return 'bg-stone-600'
  }
}

function captureStatusColor(status: string): string {
  switch (status) {
    case 'completed': return 'text-green-400'
    case 'processing': return 'text-amber-400'
    case 'failed': return 'text-red-400'
    default: return 'text-stone-400'
  }
}

function captureStatusLabel(status: string): string {
  switch (status) {
    case 'completed': return 'Complete'
    case 'processing': return 'Processing...'
    case 'failed': return 'Failed'
    default: return 'Pending'
  }
}
</script>

<template>
  <div v-if="isLoading" class="p-4 text-stone-500 text-center py-12">Loading...</div>

  <div v-else-if="!capture" class="p-4 text-stone-500 text-center py-12">
    <p>Capture not found.</p>
    <button @click="router.push('/history')" class="text-amber-500 mt-2">Back to History</button>
  </div>

  <div v-else class="p-4 max-w-lg mx-auto">
    <button @click="router.push('/history')" class="text-stone-400 hover:text-stone-200 text-sm mb-4">
      Back to History
    </button>

    <!-- Photos -->
    <div v-if="capture.photos.length" class="mb-4 -mx-4">
      <div class="flex gap-2 overflow-x-auto px-4 pb-2">
        <img
          v-for="(url, i) in capture.photos"
          :key="i"
          :src="url"
          class="h-40 object-cover rounded-xl"
        />
      </div>
    </div>

    <!-- Status banner -->
    <div class="flex items-center justify-between mb-4">
      <span :class="captureStatusColor(capture.status)" class="text-sm font-medium">
        {{ captureStatusLabel(capture.status) }}
      </span>
      <div class="flex items-center gap-3">
        <button
          v-if="canReprocess"
          @click="reprocess"
          :disabled="isReprocessing"
          class="text-xs px-3 py-1.5 rounded-lg bg-amber-700 hover:bg-amber-600 disabled:bg-stone-700 disabled:text-stone-500 text-white transition-colors"
        >
          {{ isReprocessing ? 'Reprocessing...' : 'Rerun AI Workflow' }}
        </button>
        <span class="text-xs text-stone-600">
          {{ new Date(capture.createdAt).toLocaleString() }}
        </span>
      </div>
    </div>

    <p v-if="capture.userNote" class="text-sm text-stone-400 mb-4 italic">"{{ capture.userNote }}"</p>

    <!-- Workflow Steps Timeline -->
    <div v-if="capture.workflowSteps.length" class="mb-6">
      <h3 class="text-sm font-medium text-stone-400 mb-1">
        {{ usedAi ? 'AI Agent Workflow' : 'Processing Workflow' }}
      </h3>
      <p v-if="usedAi" class="text-xs text-stone-600 mb-3">
        Steps below were executed by AI agents in Azure AI Foundry.
      </p>
      <p v-else class="text-xs text-yellow-600 mb-3">
        AI agents were not available. Results below are from local keyword extraction only.
      </p>

      <div class="relative">
        <div class="absolute left-3 top-3 bottom-3 w-0.5 bg-stone-800"></div>

        <div
          v-for="(step, i) in capture.workflowSteps"
          :key="i"
          class="relative pl-10 pb-4 last:pb-0"
        >
          <div class="absolute left-1.5 top-1.5 w-3 h-3 rounded-full" :class="dotColor(step.status)"></div>

          <div class="bg-stone-900 border rounded-xl p-3" :class="statusColor(step.status)">
            <div class="flex items-center justify-between mb-1">
              <div class="flex items-center gap-2">
                <span
                  class="text-[10px] font-semibold uppercase tracking-wider px-1.5 py-0.5 rounded"
                  :class="isAiStep(step)
                    ? 'bg-amber-900/40 text-amber-400 border border-amber-800/50'
                    : 'bg-stone-800 text-stone-500 border border-stone-700'"
                >
                  {{ isAiStep(step) ? 'AI Agent' : 'Local' }}
                </span>
                <span class="text-sm font-medium">{{ step.agentName }}</span>
              </div>
              <div class="flex items-center gap-2">
                <span class="text-xs" :class="statusColor(step.status)">{{ statusLabel(step.status) }}</span>
                <span class="text-xs text-stone-700">{{ step.stepId }}</span>
              </div>
            </div>
            <p v-if="step.summary" class="text-xs text-stone-400 leading-relaxed">{{ step.summary }}</p>
            <details v-if="step.detail && step.detail !== step.summary" class="mt-2">
              <summary class="text-xs text-stone-600 cursor-pointer hover:text-stone-400">Show full output</summary>
              <pre class="mt-2 text-xs text-stone-500 bg-stone-950 rounded-lg p-3 overflow-x-auto whitespace-pre-wrap max-h-48 overflow-y-auto">{{ step.detail }}</pre>
            </details>
          </div>
        </div>
      </div>
    </div>

    <!-- Processing error -->
    <div v-if="capture.processingError" class="bg-red-950/30 border border-red-900 rounded-xl p-3 mb-4">
      <p class="text-xs font-medium text-red-400 mb-1">Processing Error</p>
      <p class="text-xs text-red-400/80">{{ capture.processingError }}</p>
    </div>

    <!-- Resulting Items -->
    <div v-if="items.length" class="mb-4">
      <h3 class="text-sm font-medium text-stone-400 mb-3">Identified Items</h3>
      <div class="space-y-2">
        <router-link
          v-for="item in items"
          :key="item.id"
          :to="`/items/${item.id}`"
          class="block bg-stone-900 border border-stone-800 hover:border-stone-700 rounded-xl p-3 transition-colors"
        >
          <div class="flex items-center gap-3">
            <img
              v-if="item.photoUrls.length"
              :src="item.photoUrls[0]"
              class="w-12 h-12 object-cover rounded-lg shrink-0"
            />
            <div v-else class="w-12 h-12 bg-stone-800 rounded-lg shrink-0 flex items-center justify-center text-xs text-stone-500 uppercase">
              {{ item.type }}
            </div>
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 mb-0.5">
                <span class="text-xs px-2 py-0.5 rounded-full bg-stone-800 text-stone-400">{{ item.type }}</span>
                <span v-if="item.aiConfidence" class="text-xs text-stone-600">{{ Math.round(item.aiConfidence * 100) }}%</span>
              </div>
              <h4 class="font-medium text-stone-100 truncate text-sm">{{ item.name }}</h4>
              <p v-if="item.brand" class="text-xs text-stone-500 truncate">{{ item.brand }} {{ item.category ? `/ ${item.category}` : '' }}</p>
            </div>
            <span class="text-stone-600 text-sm">&rarr;</span>
          </div>
        </router-link>
      </div>
    </div>

    <!-- Empty state while processing -->
    <div v-else-if="capture.status === 'processing'" class="text-center py-6">
      <div class="inline-block w-5 h-5 border-2 border-stone-600 border-t-amber-500 rounded-full animate-spin mb-3"></div>
      <p class="text-stone-500 text-sm">Processing your capture...</p>
    </div>
  </div>
</template>
