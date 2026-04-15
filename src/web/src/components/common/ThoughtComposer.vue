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
  <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
    <textarea
      v-model="content"
      placeholder="Share your thoughts..."
      maxlength="500"
      rows="3"
      class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c] resize-none"
    ></textarea>

    <div class="flex items-center justify-between">
      <div v-if="props.showRating" class="flex items-center gap-2">
        <span class="text-xs text-[#5a8ab5]">Rate:</span>
        <button
          v-for="star in 5"
          :key="star"
          @click="rating = rating === star ? undefined : star"
          class="text-lg"
          :class="rating && star <= rating ? 'text-[#96BEE6]' : 'text-[#1e407c]/50'"
        >
          &#9733;
        </button>
      </div>
      <div v-else />

      <button
        @click="submit"
        :disabled="!content.trim() || isSubmitting"
        class="bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#0a2a52] disabled:text-[#4a7aa5]/60 text-white px-4 py-2 rounded-xl text-sm font-medium transition-colors"
      >
        {{ isSubmitting ? 'Posting...' : 'Post' }}
      </button>
    </div>
    <p class="text-xs text-[#4a7aa5] text-right">{{ content.length }}/500</p>
  </div>
</template>
