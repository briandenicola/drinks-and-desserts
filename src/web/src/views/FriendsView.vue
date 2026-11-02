<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, computed } from 'vue'
import { friendsApi, type Friendship, type FriendInvite } from '../services/friends'
import { notificationsApi } from '../services/notifications'
import { useRouter } from 'vue-router'
import { useBreakpoint } from '../composables/useBreakpoint'

const router = useRouter()
const { isDesktop } = useBreakpoint()
let copyTimer: ReturnType<typeof setTimeout> | undefined
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
  clearTimeout(copyTimer)
  copyTimer = setTimeout(() => { linkCopied.value = false }, 2000)
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

onBeforeUnmount(() => {
  clearTimeout(copyTimer)
})
</script>

<template>
  <div class="p-4 mx-auto" :class="isDesktop ? 'max-w-6xl' : 'max-w-lg'">
    <div class="flex items-center justify-between mb-6">
      <button @click="router.back()" class="text-th-accent text-sm">Back</button>
      <h2 class="text-xl font-semibold">Friends</h2>
      <button @click="openInviteModal" class="text-th-accent hover:text-th-text">
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
          ? 'bg-th-brand border-th-edge text-white'
          : 'bg-th-card border-th-edge/50 text-th-accent hover:border-th-edge'"
      >
        {{ tab === 'friends' ? `Friends (${friends.length})` : 'Requests' }}
        <span v-if="tab === 'requests' && pendingCount > 0"
          class="absolute -top-1 -right-1 w-5 h-5 bg-red-500 rounded-full text-xs flex items-center justify-center text-white">
          {{ pendingCount }}
        </span>
      </button>
    </div>

    <div v-if="error" class="bg-th-danger/15 border border-th-danger/40 text-th-danger-soft rounded-xl p-3 mb-4 text-sm">
      {{ error }}
    </div>

    <div v-if="isLoading" class="text-center text-th-muted-strong py-12">Loading...</div>

    <!-- Friends tab -->
    <template v-else-if="activeTab === 'friends'">
      <!-- Friends list -->
      <div v-if="friends.length === 0" class="text-center text-th-muted-strong py-8">
        <p>No friends yet</p>
        <p class="text-sm mt-1">Tap + to generate an invite link</p>
      </div>

      <div v-else class="space-y-3">
        <div
          v-for="friend in friends"
          :key="friend.id"
          class="bg-th-inset border border-th-line rounded-xl p-4 hover:border-th-edge/50 transition-colors"
        >
          <div class="flex items-center justify-between">
            <button @click="router.push(`/friends/${friend.friendId}`)" class="flex-1 text-left">
              <h3 class="font-medium text-th-heading">{{ friend.friendDisplayName }}</h3>
              <p class="text-xs text-th-muted mt-1">Friends since {{ new Date(friend.createdAt).toLocaleDateString() }}</p>
            </button>
            <button @click="removeFriend(friend)" class="text-th-danger text-sm ml-4 hover:text-th-danger-soft">Remove</button>
          </div>
        </div>
      </div>
    </template>

    <!-- Requests tab -->
    <template v-else>
      <div v-if="receivedRequests.length > 0" class="space-y-3 mb-6">
        <h3 class="text-sm font-medium text-th-emphasis uppercase tracking-wide">Received</h3>
        <div
          v-for="req in receivedRequests"
          :key="req.id"
          class="bg-th-inset border border-th-line rounded-xl p-4"
        >
          <h3 class="font-medium text-th-heading mb-1">{{ req.friendDisplayName }}</h3>
          <div class="flex gap-2">
            <button @click="acceptRequest(req)" class="flex-1 bg-th-brand hover:bg-th-brand-hover text-white py-2 rounded-xl text-sm font-medium">
              Accept
            </button>
            <button @click="declineRequest(req)" class="flex-1 bg-th-card hover:bg-th-inset text-th-accent py-2 rounded-xl text-sm font-medium border border-th-edge/50">
              Decline
            </button>
          </div>
        </div>
      </div>

      <div v-if="sentRequests.length > 0" class="space-y-3">
        <h3 class="text-sm font-medium text-th-emphasis uppercase tracking-wide">Sent</h3>
        <div
          v-for="req in sentRequests"
          :key="req.id"
          class="bg-th-inset border border-th-line rounded-xl p-4"
        >
          <h3 class="font-medium text-th-heading">{{ req.friendDisplayName }}</h3>
          <p class="text-sm text-th-muted">Pending</p>
        </div>
      </div>

      <div v-if="receivedRequests.length === 0 && sentRequests.length === 0"
        class="text-center text-th-muted-strong py-8">
        No pending requests
      </div>
    </template>

    <!-- Invite Modal -->
    <Teleport to="body">
      <Transition name="fade">
        <div v-if="showInviteModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60" @click.self="showInviteModal = false">
          <div class="bg-th-inset border border-th-edge rounded-2xl p-6 w-full max-w-sm shadow-xl">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-semibold text-th-heading">Invite a Friend</h3>
              <button @click="showInviteModal = false" class="text-th-muted hover:text-th-text p-1">
                <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

            <div v-if="isCreatingInvite" class="text-center text-th-muted-strong py-8">
              Generating invite...
            </div>

            <div v-else-if="invite" class="space-y-4">
              <p class="text-sm text-th-muted-strong">Share this link with someone to add them as a friend.</p>

              <div class="bg-th-card rounded-xl p-4 text-center">
                <p class="text-xs text-th-muted mb-1">Invite Code</p>
                <p class="text-2xl font-mono font-bold text-th-accent tracking-[0.25em]">{{ invite.id }}</p>
              </div>

              <button
                @click="copyLink"
                class="w-full bg-th-brand hover:bg-th-brand-hover text-white py-3 rounded-xl font-medium transition-colors"
              >
                {{ linkCopied ? 'Copied' : 'Copy Invite Link' }}
              </button>

              <p class="text-xs text-th-muted text-center">
                Expires {{ new Date(invite.expiresAt).toLocaleDateString() }}
              </p>
            </div>

            <div v-else class="text-center text-th-danger py-4">
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
