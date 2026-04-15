<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { friendsApi, type Friendship } from '../services/friends'
import type { Item } from '../services/items'
import StarRating from '../components/common/StarRating.vue'

const route = useRoute()
const router = useRouter()
const friendId = computed(() => route.params.friendId as string)
const friend = ref<Friendship | null>(null)
const items = ref<Item[]>([])
const isLoading = ref(true)
const error = ref('')
const activeTab = ref<'collection' | 'venues'>('collection')

async function load() {
  isLoading.value = true
  error.value = ''
  try {
    const [friendsRes, itemsRes] = await Promise.all([
      friendsApi.list(),
      friendsApi.getFriendItems(friendId.value),
    ])
    friend.value = friendsRes.data.find(f => f.friendId === friendId.value) || null
    items.value = itemsRes.data.items || []
  } catch {
    error.value = 'Failed to load friend data'
  } finally {
    isLoading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <div class="flex items-center justify-between mb-6">
      <div>
        <button @click="router.push('/friends')" class="text-[#96BEE6] text-sm mb-1">&larr; Friends</button>
        <h2 class="text-xl font-semibold">{{ friend?.friendDisplayName || 'Friend' }}</h2>
      </div>
    </div>

    <!-- Tabs -->
    <div class="flex gap-2 mb-6">
      <button
        @click="activeTab = 'collection'"
        class="px-4 py-2.5 min-h-[44px] rounded-full text-sm border transition-colors"
        :class="activeTab === 'collection'
          ? 'bg-[#1e407c] border-[#1e407c] text-white'
          : 'bg-[#0a2a52] border-[#1e407c]/50 text-[#96BEE6] hover:border-[#1e407c]'"
      >
        Collection ({{ items.length }})
      </button>
      <button
        @click="activeTab = 'venues'; router.push(`/friends/${friendId}/venues`)"
        class="px-4 py-2.5 min-h-[44px] rounded-full text-sm border transition-colors bg-[#0a2a52] border-[#1e407c]/50 text-[#96BEE6] hover:border-[#1e407c]"
      >
        Venues
      </button>
    </div>

    <div v-if="error" class="bg-red-900/30 border border-red-700 text-red-300 rounded-xl p-3 mb-4 text-sm">
      {{ error }}
    </div>

    <div v-if="isLoading" class="text-center text-[#5a8ab5] py-12">Loading...</div>

    <div v-else-if="items.length === 0" class="text-center text-[#5a8ab5] py-12">
      <p>No items in collection yet</p>
    </div>

    <div v-else class="space-y-3">
      <router-link
        v-for="item in items"
        :key="item.id"
        :to="`/friends/${friendId}/items/${item.id}`"
        class="block bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 hover:border-[#1e407c]/50 transition-colors"
      >
        <div class="flex items-start gap-3">
          <img
            v-if="item.photoUrls?.length"
            :src="item.photoUrls[0]"
            class="w-16 h-16 object-cover rounded-lg shrink-0"
          />
          <div v-else class="w-16 h-16 bg-[#0a2a52] rounded-lg shrink-0 flex items-center justify-center text-xs text-[#96BEE6]/70 uppercase">
            {{ item.type }}
          </div>

          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2 mb-1">
              <span class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6]">{{ item.type }}</span>
            </div>
            <h3 class="font-medium text-white truncate">{{ item.name }}</h3>
            <p v-if="item.brand" class="text-sm text-[#96BEE6]/70 truncate">{{ item.brand }}</p>
            <div v-if="item.userRating" class="mt-1">
              <StarRating :rating="item.userRating" size="sm" />
            </div>
          </div>
        </div>
      </router-link>
    </div>
  </div>
</template>
