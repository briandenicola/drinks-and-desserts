<script setup lang="ts">
import { type Thought } from '../../services/thoughts'
import { useAuthStore } from '../../stores/auth'

const props = defineProps<{
  thoughts: Thought[]
}>()

const emit = defineEmits<{
  delete: [thought: Thought]
}>()

const auth = useAuthStore()
</script>

<template>
  <div class="space-y-3">
    <div v-for="thought in props.thoughts" :key="thought.id"
      class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4">
      <div class="flex items-start justify-between mb-2">
        <div>
          <span class="font-medium text-white text-sm">{{ thought.authorDisplayName }}</span>
          <span class="text-xs text-[#4a7aa5] ml-2">{{ new Date(thought.createdAt).toLocaleDateString() }}</span>
        </div>
        <button
          v-if="thought.authorId === auth.user?.id"
          @click="emit('delete', thought)"
          class="text-red-400 text-xs hover:text-red-300"
        >
          Delete
        </button>
      </div>
      <div v-if="thought.rating" class="mb-1">
        <span v-for="star in 5" :key="star"
          :class="star <= thought.rating ? 'text-[#96BEE6]' : 'text-[#1e407c]/50'">&#9733;</span>
      </div>
      <p class="text-sm text-[#96BEE6]/80">{{ thought.content }}</p>
    </div>

    <div v-if="props.thoughts.length === 0" class="text-center text-[#5a8ab5] py-4 text-sm">
      No thoughts yet. Be the first to share one.
    </div>
  </div>
</template>
