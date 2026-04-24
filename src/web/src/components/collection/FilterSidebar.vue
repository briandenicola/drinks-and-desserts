<script setup lang="ts">
import { computed } from 'vue'

export interface CollectionFilters {
  category?: string
  minRating: number
  labels: string
}

const props = defineProps<{
  modelValue: CollectionFilters
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: CollectionFilters): void
}>()

const categoryOptions = [
  { label: 'All Types', value: '' },
  { label: 'Whiskey', value: 'whiskey' },
  { label: 'Wine', value: 'wine' },
  { label: 'Cocktail', value: 'cocktail' },
  { label: 'Vodka', value: 'vodka' },
  { label: 'Gin', value: 'gin' },
  { label: 'Cigar', value: 'cigar' },
  { label: 'Dessert', value: 'dessert' },
  { label: 'Custom', value: 'custom' },
]

const ratingOptions = [
  { label: 'Any Rating', value: 0 },
  { label: '1+ Stars', value: 1 },
  { label: '2+ Stars', value: 2 },
  { label: '3+ Stars', value: 3 },
  { label: '4+ Stars', value: 4 },
  { label: '5 Stars', value: 5 },
]

const category = computed({
  get: () => props.modelValue.category ?? '',
  set: (v: string) => emit('update:modelValue', { ...props.modelValue, category: v || undefined }),
})

const minRating = computed({
  get: () => props.modelValue.minRating,
  set: (v: number) => emit('update:modelValue', { ...props.modelValue, minRating: v }),
})

const labels = computed({
  get: () => props.modelValue.labels,
  set: (v: string) => emit('update:modelValue', { ...props.modelValue, labels: v }),
})

const hasActiveFilters = computed(() =>
  !!props.modelValue.category || props.modelValue.minRating > 0 || props.modelValue.labels.trim() !== ''
)

function clearAll() {
  emit('update:modelValue', { category: undefined, minRating: 0, labels: '' })
}
</script>

<template>
  <aside class="w-60 shrink-0 bg-[#041e3e] border-r border-[#0a2a52] p-4 overflow-y-auto">
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-sm font-semibold text-white uppercase tracking-wider">Filters</h2>
      <button
        v-if="hasActiveFilters"
        @click="clearAll"
        class="text-xs text-[#96BEE6] hover:text-white transition-colors"
      >
        Clear All
      </button>
    </div>

    <!-- Category -->
    <div class="mb-5">
      <label class="block text-xs font-medium text-[#96BEE6]/80 mb-2">Category</label>
      <select
        v-model="category"
        class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-[#1e407c] appearance-none"
      >
        <option v-for="opt in categoryOptions" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </option>
      </select>
    </div>

    <!-- Min Rating -->
    <div class="mb-5">
      <label class="block text-xs font-medium text-[#96BEE6]/80 mb-2">Minimum Rating</label>
      <select
        v-model.number="minRating"
        class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-[#1e407c] appearance-none"
      >
        <option v-for="opt in ratingOptions" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </option>
      </select>
    </div>

    <!-- Labels / Tags -->
    <div class="mb-5">
      <label class="block text-xs font-medium text-[#96BEE6]/80 mb-2">Labels</label>
      <input
        v-model="labels"
        placeholder="Search tags..."
        class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-lg px-3 py-2 text-sm text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
      />
    </div>
  </aside>
</template>
