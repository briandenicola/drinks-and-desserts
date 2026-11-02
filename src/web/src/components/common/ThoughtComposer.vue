<script setup lang="ts">
import { ref } from 'vue'

const props = defineProps<{
  showRating?: boolean
}>()

const emit = defineEmits<{
  submit: [payload: { content: string; rating?: number }]
}>()

const content = ref('')
const rating = ref<number | undefined>(undefined)
const isSubmitting = ref(false)

async function submit() {
  if (!content.value.trim()) return
  isSubmitting.value = true
  try {
    emit('submit', {
      content: content.value.trim(),
      rating: props.showRating ? rating.value : undefined,
    })
    content.value = ''
    rating.value = undefined
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="bg-th-inset border border-th-line rounded-xl p-4 space-y-3">
    <textarea
      v-model="content"
      placeholder="Share your thoughts..."
      maxlength="500"
      rows="3"
      class="w-full bg-th-card border border-th-edge/50 rounded-xl px-4 py-3 text-th-text placeholder-th-muted focus:outline-none focus:border-th-edge resize-none"
    ></textarea>

    <div class="flex items-center justify-between">
      <div v-if="props.showRating" class="flex items-center gap-2">
        <span class="text-xs text-th-muted-strong">Rate:</span>
        <button
          v-for="star in 5"
          :key="star"
          @click="rating = rating === star ? undefined : star"
          class="text-lg"
          :class="rating && star <= rating ? 'text-th-accent' : 'text-th-edge/50'"
        >
          &#9733;
        </button>
      </div>
      <div v-else />

      <button
        @click="submit"
        :disabled="!content.trim() || isSubmitting"
        class="bg-th-brand hover:bg-th-brand-hover disabled:bg-th-card disabled:text-th-muted text-white px-4 py-2 rounded-xl text-sm font-medium transition-colors"
      >
        {{ isSubmitting ? 'Posting...' : 'Post' }}
      </button>
    </div>
    <p class="text-xs text-th-muted text-right">{{ content.length }}/500</p>
  </div>
</template>
