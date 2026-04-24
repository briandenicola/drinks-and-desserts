<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  selectedCount: number
  isAllSelected: boolean
}>()

const emit = defineEmits<{
  (e: 'select-all'): void
  (e: 'clear'): void
  (e: 'export'): void
  (e: 'tag', tag: string): void
}>()

const label = computed(() =>
  props.selectedCount === 0
    ? 'None selected'
    : `${props.selectedCount} item${props.selectedCount !== 1 ? 's' : ''} selected`
)
</script>

<template>
  <div
    v-if="selectedCount > 0"
    class="flex items-center gap-3 bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2"
  >
    <button
      @click="isAllSelected ? emit('clear') : emit('select-all')"
      class="text-xs text-[#96BEE6] hover:text-white transition-colors"
    >
      {{ isAllSelected ? 'Deselect All' : 'Select All' }}
    </button>

    <span class="text-sm text-[#96BEE6]/70">{{ label }}</span>

    <div class="flex-1" />

    <button
      @click="emit('export')"
      class="flex items-center gap-1 text-xs text-[#96BEE6] hover:text-white px-3 py-1.5 rounded-lg border border-[#1e407c]/50 hover:bg-[#1e407c]/30 transition-colors"
    >
      <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
      </svg>
      Export
    </button>

    <button
      @click="emit('clear')"
      class="text-xs text-[#4a7aa5] hover:text-white transition-colors"
    >
      Cancel
    </button>
  </div>
</template>
