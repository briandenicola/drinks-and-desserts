<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { friendsApi } from '../services/friends'
import { thoughtsApi, type Thought, type CreateThoughtRequest } from '../services/thoughts'
import { useAuthStore } from '../stores/auth'
import type { Venue } from '../services/venues'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const friendId = computed(() => route.params.friendId as string)
const venueId = computed(() => route.params.id as string | undefined)

const venues = ref<Venue[]>([])
const selectedVenue = ref<Venue | null>(null)
const thoughts = ref<Thought[]>([])
const isLoading = ref(true)
const error = ref('')

// Thought composer
const newThoughtContent = ref('')
const isSubmitting = ref(false)

const isDetailMode = computed(() => !!venueId.value)

async function load() {
  isLoading.value = true
  error.value = ''
  try {
    if (isDetailMode.value) {
      const [venueRes, thoughtsRes] = await Promise.all([
        friendsApi.getFriendVenue(friendId.value, venueId.value!),
        thoughtsApi.getForTarget('venue', venueId.value!, friendId.value),
      ])
      selectedVenue.value = venueRes.data
      thoughts.value = thoughtsRes.data
    } else {
      const res = await friendsApi.getFriendVenues(friendId.value)
      venues.value = res.data.items || []
    }
  } catch {
    error.value = 'Failed to load venues'
  } finally {
    isLoading.value = false
  }
}

async function submitThought() {
  if (!newThoughtContent.value.trim() || !venueId.value) return
  isSubmitting.value = true
  try {
    const req: CreateThoughtRequest = {
      content: newThoughtContent.value.trim(),
      targetUserId: friendId.value,
      targetType: 'venue',
      targetId: venueId.value,
    }
    const res = await thoughtsApi.create(req)
    thoughts.value.unshift(res.data)
    newThoughtContent.value = ''
  } catch {
    error.value = 'Failed to post thought'
  } finally {
    isSubmitting.value = false
  }
}

async function deleteThought(thought: Thought) {
  try {
    await thoughtsApi.remove(thought.id)
    thoughts.value = thoughts.value.filter(t => t.id !== thought.id)
  } catch {
    error.value = 'Failed to delete thought'
  }
}

const venueTypeLabel = (type: string) => {
  const map: Record<string, string> = { bar: 'Bar', lounge: 'Lounge', restaurant: 'Restaurant', other: 'Other' }
  return map[type] || type
}

onMounted(load)
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <button @click="router.back()" class="text-[#96BEE6] text-sm mb-4">&larr; Back</button>

    <div v-if="isLoading" class="text-center text-[#5a8ab5] py-12">Loading...</div>

    <div v-else-if="error && !selectedVenue && venues.length === 0"
      class="bg-red-900/30 border border-red-700 text-red-300 rounded-xl p-3 text-sm">
      {{ error }}
    </div>

    <!-- Venue list -->
    <template v-else-if="!isDetailMode">
      <h2 class="text-xl font-semibold mb-4">Venues</h2>

      <div v-if="venues.length === 0" class="text-center text-[#5a8ab5] py-12">
        No venues yet
      </div>

      <div v-else class="space-y-3">
        <router-link
          v-for="venue in venues"
          :key="venue.id"
          :to="`/friends/${friendId}/venues/${venue.id}`"
          class="block bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 hover:border-[#1e407c]/50 transition-colors"
        >
          <div class="flex items-start gap-3">
            <img
              v-if="venue.photoUrls?.length"
              :src="venue.photoUrls[0]"
              class="w-12 h-12 object-cover rounded-lg shrink-0"
            />
            <div v-else class="w-12 h-12 bg-[#0a2a52] rounded-lg shrink-0 flex items-center justify-center text-xs text-[#96BEE6]/70">
              {{ venueTypeLabel(venue.type).charAt(0) }}
            </div>
            <div class="flex-1 min-w-0">
              <h3 class="font-medium text-white truncate">{{ venue.name }}</h3>
              <p class="text-xs text-[#5a8ab5]">{{ venueTypeLabel(venue.type) }}</p>
              <p v-if="venue.address" class="text-xs text-[#4a7aa5] truncate">{{ venue.address }}</p>
            </div>
          </div>
        </router-link>
      </div>
    </template>

    <!-- Venue detail -->
    <template v-else-if="selectedVenue">
      <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl overflow-hidden mb-4">
        <img
          v-if="selectedVenue.photoUrls?.length"
          :src="selectedVenue.photoUrls[0]"
          class="w-full h-48 object-cover"
        />
        <div class="p-4 space-y-3">
          <h2 class="text-xl font-semibold text-white">{{ selectedVenue.name }}</h2>
          <span class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6]">
            {{ venueTypeLabel(selectedVenue.type) }}
          </span>
          <p v-if="selectedVenue.address" class="text-sm text-[#5a8ab5]">{{ selectedVenue.address }}</p>
          <a v-if="selectedVenue.website" :href="selectedVenue.website" target="_blank"
            class="text-sm text-[#96BEE6] hover:underline block">
            {{ selectedVenue.website }}
          </a>
          <div v-if="selectedVenue.rating" class="flex items-center gap-1">
            <span v-for="star in 5" :key="star"
              :class="star <= selectedVenue.rating ? 'text-[#96BEE6]' : 'text-[#1e407c]/50'">&#9733;</span>
          </div>
          <div v-if="selectedVenue.labels?.length" class="flex flex-wrap gap-1">
            <span v-for="label in selectedVenue.labels" :key="label"
              class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6]">
              {{ label }}
            </span>
          </div>
        </div>
      </div>

      <!-- Thoughts section -->
      <section class="space-y-4">
        <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">
          Thoughts ({{ thoughts.length }})
        </h3>

        <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
          <textarea
            v-model="newThoughtContent"
            placeholder="Share your thoughts about this venue..."
            maxlength="500"
            rows="3"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c] resize-none"
          ></textarea>
          <div class="flex items-center justify-between">
            <p class="text-xs text-[#4a7aa5]">{{ newThoughtContent.length }}/500</p>
            <button
              @click="submitThought"
              :disabled="!newThoughtContent.trim() || isSubmitting"
              class="bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#0a2a52] disabled:text-[#4a7aa5]/60 text-white px-4 py-2 rounded-xl text-sm font-medium transition-colors"
            >
              {{ isSubmitting ? 'Posting...' : 'Post' }}
            </button>
          </div>
        </div>

        <div v-for="thought in thoughts" :key="thought.id"
          class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4">
          <div class="flex items-start justify-between mb-2">
            <div>
              <span class="font-medium text-white text-sm">{{ thought.authorDisplayName }}</span>
              <span class="text-xs text-[#4a7aa5] ml-2">{{ new Date(thought.createdAt).toLocaleDateString() }}</span>
            </div>
            <button
              v-if="thought.authorId === auth.user?.id"
              @click="deleteThought(thought)"
              class="text-red-400 text-xs hover:text-red-300"
            >
              Delete
            </button>
          </div>
          <p class="text-sm text-[#96BEE6]/80">{{ thought.content }}</p>
        </div>

        <div v-if="thoughts.length === 0" class="text-center text-[#5a8ab5] py-4 text-sm">
          No thoughts yet. Be the first to share one.
        </div>
      </section>

      <div v-if="error" class="bg-red-900/30 border border-red-700 text-red-300 rounded-xl p-3 text-sm mt-4">
        {{ error }}
      </div>
    </template>
  </div>
</template>
