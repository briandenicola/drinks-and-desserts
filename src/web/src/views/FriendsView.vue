<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { friendsApi, type Friendship, type FriendInvite } from '../services/friends'
import { notificationsApi } from '../services/notifications'
import { useRouter } from 'vue-router'

const router = useRouter()
const friends = ref<Friendship[]>([])
const sentRequests = ref<Friendship[]>([])
const receivedRequests = ref<Friendship[]>([])
const invite = ref<FriendInvite | null>(null)
const unreadCount = ref(0)
const isLoading = ref(true)
const isCreatingInvite = ref(false)
const linkCopied = ref(false)
const error = ref('')
const activeTab = ref<'friends' | 'requests'>('friends')

const pendingCount = computed(() => receivedRequests.value.length)
const inviteLink = computed(() => invite.value ? `${window.location.origin}/friends/join/${invite.value.id}` : '')

async function load() {
  isLoading.value = true
  error.value = ''
  try {
    const [friendsRes, requestsRes, notifRes] = await Promise.all([
      friendsApi.list(),
      friendsApi.listRequests(),
      notificationsApi.list(1),
    ])
    friends.value = friendsRes.data
    sentRequests.value = requestsRes.data.sent
    receivedRequests.value = requestsRes.data.received
    unreadCount.value = notifRes.data.unreadCount
  } catch {
    error.value = 'Failed to load friends'
  } finally {
    isLoading.value = false
  }
}

async function createInvite() {
  isCreatingInvite.value = true
  try {
    const res = await friendsApi.createInvite()
    invite.value = res.data
  } catch {
    error.value = 'Failed to create invite'
  } finally {
    isCreatingInvite.value = false
  }
}

async function copyLink() {
  if (!inviteLink.value) return
  await navigator.clipboard.writeText(inviteLink.value)
  linkCopied.value = true
  setTimeout(() => { linkCopied.value = false }, 2000)
}

async function acceptRequest(friendship: Friendship) {
  try {
    await friendsApi.accept(friendship.id)
    receivedRequests.value = receivedRequests.value.filter(r => r.id !== friendship.id)
    friends.value.push({ ...friendship, status: 'accepted' })
  } catch {
    error.value = 'Failed to accept request'
  }
}

async function declineRequest(friendship: Friendship) {
  try {
    await friendsApi.decline(friendship.id)
    receivedRequests.value = receivedRequests.value.filter(r => r.id !== friendship.id)
  } catch {
    error.value = 'Failed to decline request'
  }
}

async function removeFriend(friendship: Friendship) {
  if (!confirm(`Remove ${friendship.friendDisplayName} from friends?`)) return
  try {
    await friendsApi.remove(friendship.id)
    friends.value = friends.value.filter(f => f.id !== friendship.id)
  } catch {
    error.value = 'Failed to remove friend'
  }
}

onMounted(load)
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-xl font-semibold">Friends</h2>
      <button @click="router.back()" class="text-[#96BEE6] text-sm">Back</button>
    </div>

    <!-- Tabs -->
    <div class="flex gap-2 mb-6">
      <button
        v-for="tab in (['friends', 'requests'] as const)"
        :key="tab"
        @click="activeTab = tab"
        class="px-4 py-2.5 min-h-[44px] rounded-full text-sm border transition-colors relative"
        :class="activeTab === tab
          ? 'bg-[#1e407c] border-[#1e407c] text-white'
          : 'bg-[#0a2a52] border-[#1e407c]/50 text-[#96BEE6] hover:border-[#1e407c]'"
      >
        {{ tab === 'friends' ? `Friends (${friends.length})` : 'Requests' }}
        <span v-if="tab === 'requests' && pendingCount > 0"
          class="absolute -top-1 -right-1 w-5 h-5 bg-red-500 rounded-full text-xs flex items-center justify-center text-white">
          {{ pendingCount }}
        </span>
      </button>
    </div>

    <div v-if="error" class="bg-red-900/30 border border-red-700 text-red-300 rounded-xl p-3 mb-4 text-sm">
      {{ error }}
    </div>

    <div v-if="isLoading" class="text-center text-[#5a8ab5] py-12">Loading...</div>

    <!-- Friends tab -->
    <template v-else-if="activeTab === 'friends'">
      <!-- Invite section -->
      <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 mb-4 space-y-3">
        <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Invite a Friend</h3>
        <p class="text-sm text-[#5a8ab5]">Generate a link to share with someone you want to add as a friend.</p>

        <div v-if="invite" class="space-y-3">
          <div class="bg-[#0a2a52] rounded-lg p-3">
            <p class="text-xs text-[#5a8ab5] mb-1">Invite Code</p>
            <p class="text-lg font-mono font-bold text-[#96BEE6] tracking-widest">{{ invite.id }}</p>
          </div>

          <div class="flex gap-2">
            <button
              @click="copyLink"
              class="flex-1 bg-[#1e407c] hover:bg-[#2a5299] text-white py-2.5 rounded-xl text-sm font-medium transition-colors"
            >
              {{ linkCopied ? 'Copied' : 'Copy Link' }}
            </button>
          </div>

          <p class="text-xs text-[#4a7aa5]">
            Expires {{ new Date(invite.expiresAt).toLocaleDateString() }}
          </p>
        </div>

        <button
          v-else
          @click="createInvite"
          :disabled="isCreatingInvite"
          class="w-full bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#0a2a52] text-white py-3 rounded-xl font-medium transition-colors"
        >
          {{ isCreatingInvite ? 'Creating...' : 'Generate Invite Link' }}
        </button>
      </section>

      <!-- Friends list -->
      <div v-if="friends.length === 0" class="text-center text-[#5a8ab5] py-8">
        <p>No friends yet</p>
        <p class="text-sm mt-1">Share an invite link to get started</p>
      </div>

      <div v-else class="space-y-3">
        <div
          v-for="friend in friends"
          :key="friend.id"
          class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 hover:border-[#1e407c]/50 transition-colors"
        >
          <div class="flex items-center justify-between">
            <button @click="router.push(`/friends/${friend.friendId}`)" class="flex-1 text-left">
              <h3 class="font-medium text-white">{{ friend.friendDisplayName }}</h3>
              <p v-if="friend.friendEmail" class="text-sm text-[#5a8ab5]">{{ friend.friendEmail }}</p>
              <p class="text-xs text-[#4a7aa5] mt-1">Friends since {{ new Date(friend.createdAt).toLocaleDateString() }}</p>
            </button>
            <button @click="removeFriend(friend)" class="text-red-400 text-sm ml-4 hover:text-red-300">Remove</button>
          </div>
        </div>
      </div>
    </template>

    <!-- Requests tab -->
    <template v-else>
      <div v-if="receivedRequests.length > 0" class="space-y-3 mb-6">
        <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Received</h3>
        <div
          v-for="req in receivedRequests"
          :key="req.id"
          class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"
        >
          <h3 class="font-medium text-white mb-1">{{ req.friendDisplayName }}</h3>
          <p v-if="req.friendEmail" class="text-sm text-[#5a8ab5] mb-3">{{ req.friendEmail }}</p>
          <div class="flex gap-2">
            <button @click="acceptRequest(req)" class="flex-1 bg-[#1e407c] hover:bg-[#2a5299] text-white py-2 rounded-xl text-sm font-medium">
              Accept
            </button>
            <button @click="declineRequest(req)" class="flex-1 bg-[#0a2a52] hover:bg-[#041e3e] text-[#96BEE6] py-2 rounded-xl text-sm font-medium border border-[#1e407c]/50">
              Decline
            </button>
          </div>
        </div>
      </div>

      <div v-if="sentRequests.length > 0" class="space-y-3">
        <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Sent</h3>
        <div
          v-for="req in sentRequests"
          :key="req.id"
          class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"
        >
          <h3 class="font-medium text-white">{{ req.friendDisplayName }}</h3>
          <p class="text-sm text-[#4a7aa5]">Pending</p>
        </div>
      </div>

      <div v-if="receivedRequests.length === 0 && sentRequests.length === 0"
        class="text-center text-[#5a8ab5] py-8">
        No pending requests
      </div>
    </template>
  </div>
</template>
