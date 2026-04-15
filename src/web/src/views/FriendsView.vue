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
const showInviteModal = ref(false)

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

async function openInviteModal() {
  showInviteModal.value = true
  if (!invite.value) {
    await createInvite()
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
      <button @click="router.back()" class="text-[#96BEE6] text-sm">Back</button>
      <h2 class="text-xl font-semibold">Friends</h2>
      <button @click="openInviteModal" class="text-[#96BEE6] hover:text-white">
        <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z" />
        </svg>
      </button>
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
      <!-- Friends list -->
      <div v-if="friends.length === 0" class="text-center text-[#5a8ab5] py-8">
        <p>No friends yet</p>
        <p class="text-sm mt-1">Tap + to generate an invite link</p>
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

    <!-- Invite Modal -->
    <Teleport to="body">
      <Transition name="fade">
        <div v-if="showInviteModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60" @click.self="showInviteModal = false">
          <div class="bg-[#041e3e] border border-[#1e407c] rounded-2xl p-6 w-full max-w-sm shadow-xl">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-semibold text-white">Invite a Friend</h3>
              <button @click="showInviteModal = false" class="text-[#4a7aa5] hover:text-white p-1">
                <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

            <div v-if="isCreatingInvite" class="text-center text-[#5a8ab5] py-8">
              Generating invite...
            </div>

            <div v-else-if="invite" class="space-y-4">
              <p class="text-sm text-[#5a8ab5]">Share this link with someone to add them as a friend.</p>

              <div class="bg-[#0a2a52] rounded-xl p-4 text-center">
                <p class="text-xs text-[#4a7aa5] mb-1">Invite Code</p>
                <p class="text-2xl font-mono font-bold text-[#96BEE6] tracking-[0.25em]">{{ invite.id }}</p>
              </div>

              <button
                @click="copyLink"
                class="w-full bg-[#1e407c] hover:bg-[#2a5299] text-white py-3 rounded-xl font-medium transition-colors"
              >
                {{ linkCopied ? 'Copied' : 'Copy Invite Link' }}
              </button>

              <p class="text-xs text-[#4a7aa5] text-center">
                Expires {{ new Date(invite.expiresAt).toLocaleDateString() }}
              </p>
            </div>

            <div v-else class="text-center text-red-400 py-4">
              Failed to generate invite. Try again.
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
