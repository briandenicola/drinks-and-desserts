<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { notificationsApi, type AppNotification } from '../../services/notifications'

const router = useRouter()
const unreadCount = ref(0)
const showDropdown = ref(false)
const notifications = ref<AppNotification[]>([])
const isLoading = ref(false)

async function loadCount() {
  try {
    const res = await notificationsApi.list(1)
    unreadCount.value = res.data.unreadCount
  } catch { /* silent */ }
}

async function toggleDropdown() {
  showDropdown.value = !showDropdown.value
  if (showDropdown.value) {
    isLoading.value = true
    try {
      const res = await notificationsApi.list(20)
      notifications.value = res.data.notifications
      unreadCount.value = res.data.unreadCount
    } catch { /* silent */ }
    isLoading.value = false
  }
}

async function markAllRead() {
  try {
    await notificationsApi.markAllRead()
    notifications.value.forEach(n => n.isRead = true)
    unreadCount.value = 0
  } catch { /* silent */ }
}

async function handleNotificationClick(n: AppNotification) {
  if (!n.isRead) {
    try {
      await notificationsApi.markRead(n.id)
      n.isRead = true
      unreadCount.value = Math.max(0, unreadCount.value - 1)
    } catch { /* silent */ }
  }

  showDropdown.value = false

  if (n.type === 'friend-request' || n.type === 'friend-accepted') {
    router.push('/friends')
  } else if (n.type === 'new-thought') {
    if (n.referenceType && n.referenceId) {
      // Navigate to the item/venue that got the thought
      router.push(`/${n.referenceType}s/${n.referenceId}`)
    }
  } else if (n.type === 'workflow-completed') {
    // Navigate to the relevant resource or collection
    if (n.referenceType === 'venue' && n.referenceId) {
      router.push(`/venues/${n.referenceId}`)
    } else if (n.referenceType === 'item' && n.referenceId) {
      router.push(`/items/${n.referenceId}`)
    } else {
      router.push('/collection')
    }
  }
}

function closeDropdown(e: MouseEvent) {
  const target = e.target as HTMLElement
  if (!target.closest('.notification-bell-container')) {
    showDropdown.value = false
  }
}

let refreshInterval: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  loadCount()
  refreshInterval = setInterval(loadCount, 60000)
  window.addEventListener('click', closeDropdown)
})

onUnmounted(() => {
  if (refreshInterval) clearInterval(refreshInterval)
  window.removeEventListener('click', closeDropdown)
})
</script>

<template>
  <div class="notification-bell-container relative">
    <button @click.stop="toggleDropdown" class="relative p-1 text-[#96BEE6] hover:text-white transition-colors">
      <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
      </svg>
      <span v-if="unreadCount > 0"
        class="absolute -top-1 -right-1 w-4 h-4 bg-red-500 rounded-full text-[10px] flex items-center justify-center text-white font-bold">
        {{ unreadCount > 9 ? '9+' : unreadCount }}
      </span>
    </button>

    <!-- Dropdown -->
    <div v-if="showDropdown"
      class="absolute right-0 top-8 w-80 max-h-96 overflow-y-auto bg-[#041e3e] border border-[#0a2a52] rounded-xl shadow-xl z-50">
      <div class="flex items-center justify-between p-3 border-b border-[#0a2a52]">
        <h3 class="text-sm font-medium text-white">Notifications</h3>
        <button v-if="unreadCount > 0" @click="markAllRead" class="text-xs text-[#96BEE6] hover:text-white">
          Mark all read
        </button>
      </div>

      <div v-if="isLoading" class="p-4 text-center text-[#5a8ab5] text-sm">Loading...</div>

      <div v-else-if="notifications.length === 0" class="p-4 text-center text-[#5a8ab5] text-sm">
        No notifications yet
      </div>

      <div v-else>
        <button
          v-for="n in notifications"
          :key="n.id"
          @click="handleNotificationClick(n)"
          class="w-full text-left p-3 border-b border-[#0a2a52]/50 hover:bg-[#0a2a52]/50 transition-colors"
          :class="n.isRead ? 'opacity-60' : ''"
        >
          <div class="flex items-start gap-2">
            <span v-if="!n.isRead" class="w-2 h-2 bg-[#96BEE6] rounded-full mt-1.5 shrink-0"></span>
            <div class="flex-1 min-w-0">
              <p class="text-sm text-white">{{ n.title }}</p>
              <p v-if="n.detail" class="text-xs text-[#5a8ab5] truncate mt-0.5">{{ n.detail }}</p>
              <p class="text-xs text-[#4a7aa5] mt-1">{{ new Date(n.createdAt).toLocaleDateString() }}</p>
            </div>
          </div>
        </button>
      </div>
    </div>
  </div>
</template>
