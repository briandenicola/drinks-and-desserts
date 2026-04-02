<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'

const props = defineProps<{
  modelValue: string
  suggestions: string[]
  placeholder?: string
  inputClass?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const showDropdown = ref(false)
const wrapperRef = ref<HTMLDivElement>()

const filtered = computed(() => {
  const q = props.modelValue.toLowerCase().trim()
  if (!q) return props.suggestions.slice(0, 8)
  return props.suggestions
    .filter(s => s.toLowerCase().includes(q))
    .slice(0, 8)
})

function select(value: string) {
  emit('update:modelValue', value)
  showDropdown.value = false
}

function onInput(e: Event) {
  emit('update:modelValue', (e.target as HTMLInputElement).value)
  showDropdown.value = true
}

function onFocus() {
  if (props.suggestions.length) showDropdown.value = true
}

function closeOnClickOutside(e: MouseEvent) {
  if (wrapperRef.value && !wrapperRef.value.contains(e.target as Node)) {
    showDropdown.value = false
  }
}

onMounted(() => document.addEventListener('click', closeOnClickOutside))
onUnmounted(() => document.removeEventListener('click', closeOnClickOutside))
</script>

<template>
  <div ref="wrapperRef" class="relative">
    <input
      ref="inputRef"
      :value="modelValue"
      :placeholder="placeholder"
      :class="inputClass || 'w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700'"
      @input="onInput"
      @focus="onFocus"
    />
    <div
      v-if="showDropdown && filtered.length"
      class="absolute left-0 right-0 top-full mt-1 bg-stone-900 border border-stone-700 rounded-xl overflow-hidden shadow-lg z-20 max-h-48 overflow-y-auto"
    >
      <button
        v-for="item in filtered"
        :key="item"
        @mousedown.prevent="select(item)"
        class="w-full text-left px-4 py-2.5 text-sm text-stone-300 hover:bg-stone-800 transition-colors truncate"
      >
        {{ item }}
      </button>
    </div>
  </div>
</template>
