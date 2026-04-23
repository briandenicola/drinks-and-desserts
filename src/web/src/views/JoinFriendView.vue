<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { friendsApi } from '../services/friends'
import { useAuthStore } from '../stores/auth'
import { getErrorMessage } from '../services/errors'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const code = computed(() => (route.params.code as string).toUpperCase())
const isJoining = ref(false)
const isLoading = ref(true)
const success = ref(false)
const friendName = ref('')
const error = ref('')

async function join() {
  isJoining.value = true
  error.value = ''
  try {
    const res = await friendsApi.joinViaInvite(code.value)
    friendName.value = res.data.friendDisplayName
    success.value = true
  } catch (e: unknown) {
    error.value = getErrorMessage(e, 'Failed to join. The invite may be expired or invalid.')
  } finally {
    isJoining.value = false
  }
}

onMounted(() => {
  isLoading.value = false
  if (!auth.isAuthenticated) {
    // They'll be redirected to login; after login they'll come back here
    return
  }
})
</script>

<template>
  <div class="p-4 max-w-lg mx-auto flex flex-col items-center justify-center min-h-[60vh]">
    <template v-if="success">
      <div class="text-center space-y-4">
        <div class="w-16 h-16 bg-[#1e407c] rounded-full flex items-center justify-center mx-auto">
          <svg class="w-8 h-8 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
          </svg>
        </div>
        <h2 class="text-xl font-semibold text-white">You are now friends with {{ friendName }}</h2>
        <p class="text-[#5a8ab5]">You can now view each other's collections and share thoughts.</p>
        <button
          @click="router.push('/friends')"
          class="bg-[#1e407c] hover:bg-[#2a5299] text-white px-6 py-3 rounded-xl font-medium transition-colors"
        >
          View Friends
        </button>
      </div>
    </template>

    <template v-else>
      <div class="text-center space-y-6">
        <h2 class="text-xl font-semibold text-white">Friend Invite</h2>
        <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-6 space-y-4">
          <p class="text-[#96BEE6]">You've been invited to connect as friends.</p>
          <div class="bg-[#0a2a52] rounded-lg p-3">
            <p class="text-xs text-[#5a8ab5] mb-1">Invite Code</p>
            <p class="text-lg font-mono font-bold text-[#96BEE6] tracking-widest">{{ code }}</p>
          </div>

          <div v-if="error" class="bg-red-900/30 border border-red-700 text-red-300 rounded-xl p-3 text-sm">
            {{ error }}
          </div>

          <button
            @click="join"
            :disabled="isJoining"
            class="w-full bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#0a2a52] text-white py-3 rounded-xl font-medium transition-colors"
          >
            {{ isJoining ? 'Joining...' : 'Accept Invite' }}
          </button>
        </div>
      </div>
    </template>
  </div>
</template>
