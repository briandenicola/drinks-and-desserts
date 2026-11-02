<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  rating: number
  size?: 'sm' | 'md' | 'lg'
  interactive?: boolean
}>(), {
  size: 'md',
  interactive: false,
})

const emit = defineEmits<{
  (e: 'update:rating', value: number): void
}>()

const svgSizeClass = computed(() => ({
  sm: 'w-3.5 h-3.5',
  md: 'w-5 h-5',
  lg: 'w-7 h-7',
}[props.size]))

const tapTargetClass = computed(() => ({
  sm: '',
  md: '',
  lg: 'p-1.5',
}[props.size]))

const gapClass = computed(() => ({
  sm: 'gap-0.5',
  md: 'gap-0.5',
  lg: 'gap-0',
}[props.size]))

const stars = computed(() => {
  const result: number[] = []
  let remaining = props.rating
  for (let i = 0; i < 5; i++) {
    if (remaining >= 1) {
      result.push(1)
      remaining -= 1
    } else if (remaining > 0) {
      result.push(Math.round(remaining * 4) / 4)
      remaining = 0
    } else {
      result.push(0)
    }
  }
  return result
})

function handleTap(starIndex: number, event: PointerEvent) {
  if (!props.interactive) return
  const target = (event.currentTarget as HTMLElement).querySelector('svg')
  if (!target) return
  const rect = target.getBoundingClientRect()
  const x = event.clientX - rect.left
  const fraction = Math.max(0, Math.min(1, x / rect.width))
  let quarter = Math.ceil(fraction * 4) / 4
  if (quarter < 0.25) quarter = 0.25
  const value = starIndex + quarter
  emit('update:rating', value)
}

function clipId(starIdx: number) {
  return `star-clip-${starIdx}-${Math.round(stars.value[starIdx] * 100)}`
}
</script>

<template>
  <div
    class="flex items-center"
    :class="[gapClass, interactive ? 'cursor-pointer select-none' : '']"
    :style="interactive ? 'touch-action: manipulation' : ''"
  >
    <div
      v-for="(fill, i) in stars"
      :key="i"
      :class="tapTargetClass"
      @pointerup="handleTap(i, $event)"
    >
      <svg
        :class="svgSizeClass"
        viewBox="0 0 20 20"
        style="pointer-events: none"
      >
        <defs>
          <clipPath :id="clipId(i)">
            <rect x="0" y="0" :width="fill * 20" height="20" />
          </clipPath>
        </defs>
        <path
          class="text-th-muted/40"
          fill="currentColor"
          d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z"
        />
        <path
          v-if="fill > 0"
          class="text-th-accent"
          fill="currentColor"
          :clip-path="`url(#${clipId(i)})`"
          d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z"
        />
      </svg>
    </div>
  </div>
</template>
