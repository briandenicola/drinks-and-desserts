<script setup lang="ts">
import { ref, watch } from 'vue'
import { friendsApi, type Friendship } from '../../services/friends'

const props = defineProps<{
  modelValue: boolean
  itemName: string
  isSharing?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  select: [friendId: string]
}>()

const friends = ref<Friendship[]>([])
const isLoading = ref(false)
const error = ref('')

async function loadFriends() {
  isLoading.value = true
  error.value = ''
  try {
    const { data } = await friendsApi.list()
    friends.value = data
  } catch {
    error.value = 'Failed to load friends'
  } finally {
    isLoading.value = false
  }
}

watch(() => props.modelValue, (open) => {
  if (open) {
    error.value = ''
    loadFriends()
  }
})

function close() {
  emit('update:modelValue', false)
}
</script>

<template>
  <Teleport to="body">
    <div v-if="modelValue" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="close">
      <div class="bg-[#041e3e] border border-[#1e407c]/50 rounded-2xl p-6 w-full max-w-sm">
        <div class="flex items-center justify-between mb-4">
          <h3 class="text-lg font-semibold text-white truncate pr-2">Share "{{ itemName }}"</h3>
          <button @click="close" class="text-[#96BEE6] hover:text-white min-h-[44px] min-w-[44px] flex items-center justify-center shrink-0" title="Close">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div v-if="isLoading" class="text-center text-[#5a8ab5] py-6 text-sm">Loading friends...</div>

        <div v-else-if="friends.length === 0" class="text-center text-[#5a8ab5] py-6 text-sm">
          You don't have any friends yet.
        </div>

        <div v-else class="space-y-2 max-h-72 overflow-y-auto">
          <button
            v-for="friend in friends"
            :key="friend.id"
            @click="emit('select', friend.friendId)"
            :disabled="isSharing"
            class="w-full text-left min-h-[44px] px-4 py-3 rounded-xl bg-[#0a2a52] border border-[#1e407c]/50 hover:border-[#1e407c] disabled:opacity-60 text-white transition-colors"
          >
            {{ friend.friendDisplayName }}
          </button>
        </div>

        <div v-if="error" class="bg-red-900/30 border border-red-700 text-red-300 rounded-xl p-3 text-sm mt-4">
          {{ error }}
        </div>
      </div>
    </div>
  </Teleport>
</template>
