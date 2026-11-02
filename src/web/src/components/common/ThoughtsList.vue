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
      class="bg-th-inset border border-th-line rounded-xl p-4">
      <div class="flex items-start justify-between mb-2">
        <div>
          <span class="font-medium text-th-text text-sm">{{ thought.authorDisplayName }}</span>
          <span class="text-xs text-th-muted ml-2">{{ new Date(thought.createdAt).toLocaleDateString() }}</span>
        </div>
        <button
          v-if="thought.authorId === auth.user?.id"
          @click="emit('delete', thought)"
          class="text-th-danger text-xs hover:text-th-danger-soft"
        >
          Delete
        </button>
      </div>
      <div v-if="thought.rating" class="mb-1">
        <span v-for="star in 5" :key="star"
          :class="star <= thought.rating ? 'text-th-accent' : 'text-th-edge/50'">&#9733;</span>
      </div>
      <p class="text-sm text-th-accent/80">{{ thought.content }}</p>
    </div>

    <div v-if="props.thoughts.length === 0" class="text-center text-th-muted-strong py-4 text-sm">
      No thoughts yet. Be the first to share one.
    </div>
  </div>
</template>
