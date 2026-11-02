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
    case 'complete': return 'text-th-success border-th-success/40'
    case 'running': return 'text-th-accent border-th-edge/50'
    case 'error': return 'text-th-danger border-th-danger/40'
    default: return 'text-th-accent/70 border-th-line'
  }
}

function dotColor(status: string) {
  switch (status) {
    case 'complete': return 'bg-th-success'
    case 'running': return 'bg-th-emphasis animate-pulse'
    case 'error': return 'bg-th-danger'
    default: return 'bg-th-brand'
  }
}

function captureStatusColor(status: string): string {
  switch (status) {
    case 'completed': return 'text-th-success'
    case 'processing': return 'text-th-accent'
    case 'failed': return 'text-th-danger'
    default: return 'text-th-accent'
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
  <div v-if="isLoading" class="p-4 text-th-accent/70 text-center py-12">Loading...</div>

  <div v-else-if="!capture" class="p-4 text-th-accent/70 text-center py-12">
    <p>Capture not found.</p>
    <button @click="router.push('/history')" class="text-th-accent mt-2 min-h-[44px] min-w-[44px] px-3 py-2 hover:opacity-80 transition-opacity">Back to History</button>
  </div>

  <div v-else class="p-4 max-w-lg mx-auto">
    <button @click="router.push('/history')" class="text-th-accent hover:text-th-text hover:opacity-80 text-sm mb-4 min-h-[44px] min-w-[44px] px-3 py-2 transition-opacity">
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
          class="text-xs px-3 py-1.5 rounded-lg bg-th-brand hover:bg-th-brand-hover disabled:bg-th-card disabled:text-th-muted text-white transition-colors"
        >
          {{ isReprocessing ? 'Reprocessing...' : 'Rerun AI Workflow' }}
        </button>
        <span class="text-xs text-th-muted/60">
          {{ new Date(capture.createdAt).toLocaleString() }}
        </span>
      </div>
    </div>

    <p v-if="capture.userNote" class="text-sm text-th-accent mb-4 italic">"{{ capture.userNote }}"</p>

    <!-- Workflow Steps Timeline -->
    <div v-if="capture.workflowSteps.length" class="mb-6">
      <h3 class="text-sm font-medium text-th-emphasis mb-1">
        {{ usedAi ? 'AI Agent Workflow' : 'Processing Workflow' }}
      </h3>
      <p v-if="usedAi" class="text-xs text-th-muted/60 mb-3">
        Steps below were executed by AI agents in Azure AI Foundry.
      </p>
      <p v-else class="text-xs text-th-warning mb-3">
        AI agents were not available. Results below are from local keyword extraction only.
      </p>

      <div class="relative">
        <div class="absolute left-3 top-3 bottom-3 w-0.5 bg-th-card"></div>

        <div
          v-for="(step, i) in capture.workflowSteps"
          :key="i"
          class="relative pl-10 pb-4 last:pb-0"
        >
          <div class="absolute left-1.5 top-1.5 w-3 h-3 rounded-full" :class="dotColor(step.status)"></div>

          <div class="bg-th-inset border rounded-xl p-3" :class="statusColor(step.status)">
            <div class="flex items-center justify-between mb-1">
              <div class="flex items-center gap-2">
                <span
                  class="text-[10px] font-semibold uppercase tracking-wider px-1.5 py-0.5 rounded"
                  :class="isAiStep(step)
                    ? 'bg-th-brand/40 text-th-accent border border-th-edge/50'
                    : 'bg-th-card text-th-accent/70 border border-th-edge/50'"
                >
                  {{ isAiStep(step) ? 'AI Agent' : 'Local' }}
                </span>
                <span class="text-sm font-medium">{{ step.agentName }}</span>
              </div>
              <div class="flex items-center gap-2">
                <span class="text-xs" :class="statusColor(step.status)">{{ statusLabel(step.status) }}</span>
                <span class="text-xs text-th-muted/40">{{ step.stepId }}</span>
              </div>
            </div>
            <p v-if="step.summary" class="text-xs text-th-accent leading-relaxed">{{ step.summary }}</p>
            <details v-if="step.detail && step.detail !== step.summary" class="mt-2">
              <summary class="text-xs text-th-muted/60 cursor-pointer hover:text-th-accent">Show full output</summary>
              <pre class="mt-2 text-xs text-th-accent/70 bg-th-page rounded-lg p-3 overflow-x-auto whitespace-pre-wrap max-h-48 overflow-y-auto">{{ step.detail }}</pre>
            </details>
          </div>
        </div>
      </div>
    </div>

    <!-- Processing error -->
    <div v-if="capture.processingError" class="bg-th-danger/15 border border-th-danger/40 rounded-xl p-3 mb-4">
      <p class="text-xs font-medium text-th-danger mb-1">Processing Error</p>
      <p class="text-xs text-th-danger/80">{{ capture.processingError }}</p>
    </div>

    <!-- Resulting Items -->
    <div v-if="items.length" class="mb-4">
      <h3 class="text-sm font-medium text-th-emphasis mb-3">Identified Items</h3>
      <div class="space-y-2">
        <router-link
          v-for="item in items"
          :key="item.id"
          :to="`/items/${item.id}`"
          class="block bg-th-inset border border-th-line hover:border-th-edge/50 rounded-xl p-3 transition-colors"
        >
          <div class="flex items-center gap-3">
            <img
              v-if="item.photoUrls.length"
              :src="item.photoUrls[0]"
              class="w-12 h-12 object-cover rounded-lg shrink-0"
            />
            <div v-else class="w-12 h-12 bg-th-card rounded-lg shrink-0 flex items-center justify-center text-xs text-th-accent/70 uppercase">
              {{ item.type }}
            </div>
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 mb-0.5">
                <span class="text-xs px-2 py-0.5 rounded-full bg-th-card text-th-accent">{{ item.type }}</span>
                <span v-if="item.aiConfidence" class="text-xs text-th-muted/60">{{ Math.round(item.aiConfidence * 100) }}%</span>
              </div>
              <h4 class="font-medium text-th-heading truncate text-sm">{{ item.name }}</h4>
              <p v-if="item.brand" class="text-xs text-th-accent/70 truncate">{{ item.brand }} {{ item.category ? `/ ${item.category}` : '' }}</p>
            </div>
            <span class="text-th-muted/60 text-sm">&rarr;</span>
          </div>
        </router-link>
      </div>
    </div>

    <!-- Empty state while processing -->
    <div v-else-if="capture.status === 'processing'" class="text-center py-6">
      <div class="inline-block w-5 h-5 border-2 border-th-edge border-t-th-emphasis rounded-full animate-spin mb-3"></div>
      <p class="text-th-accent/70 text-sm">Processing your capture...</p>
    </div>
  </div>
</template>
