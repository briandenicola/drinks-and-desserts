<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { friendsApi } from '../services/friends'
import { thoughtsApi, type Thought, type CreateThoughtRequest } from '../services/thoughts'
import { useAuthStore } from '../stores/auth'
import type { Item } from '../services/items'
import StarRating from '../components/common/StarRating.vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const friendId = computed(() => route.params.friendId as string)
const itemId = computed(() => route.params.id as string)

const item = ref<Item | null>(null)
const thoughts = ref<Thought[]>([])
const isLoading = ref(true)
const error = ref('')

// Thought composer
const newThoughtContent = ref('')
const newThoughtRating = ref<number | undefined>(undefined)
const isSubmitting = ref(false)

async function load() {
  isLoading.value = true
  error.value = ''
  try {
    const [itemRes, thoughtsRes] = await Promise.all([
      friendsApi.getFriendItem(friendId.value, itemId.value),
      thoughtsApi.getForTarget('item', itemId.value, friendId.value),
    ])
    item.value = itemRes.data
    thoughts.value = thoughtsRes.data
  } catch {
    error.value = 'Failed to load item'
  } finally {
    isLoading.value = false
  }
}

async function submitThought() {
  if (!newThoughtContent.value.trim()) return
  isSubmitting.value = true
  try {
    const req: CreateThoughtRequest = {
      content: newThoughtContent.value.trim(),
      targetUserId: friendId.value,
      targetType: 'item',
      targetId: itemId.value,
      rating: newThoughtRating.value,
    }
    const res = await thoughtsApi.create(req)
    thoughts.value.unshift(res.data)
    newThoughtContent.value = ''
    newThoughtRating.value = undefined
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

async function saveToWishlist() {
  if (!item.value) return
  try {
    const { default: api } = await import('../services/api')
    await api.post('/api/items/wishlist', {
      name: item.value.name,
      type: item.value.type,
      brand: item.value.brand,
      notes: `From ${route.params.friendId}'s collection`,
      tags: item.value.tags,
    })
    alert('Added to your wishlist')
  } catch {
    error.value = 'Failed to save to wishlist'
  }
}

onMounted(load)
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <button @click="router.back()" class="text-[#96BEE6] text-sm mb-4">&larr; Back</button>

    <div v-if="isLoading" class="text-center text-[#5a8ab5] py-12">Loading...</div>

    <div v-else-if="error && !item" class="bg-red-900/30 border border-red-700 text-red-300 rounded-xl p-3 text-sm">
      {{ error }}
    </div>

    <template v-else-if="item">
      <!-- Item detail -->
      <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl overflow-hidden mb-4">
        <img
          v-if="item.photoUrls?.length"
          :src="item.photoUrls[0]"
          class="w-full h-48 object-cover"
        />

        <div class="p-4 space-y-3">
          <div class="flex items-center gap-2">
            <span class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6]">{{ item.type }}</span>
            <span v-if="item.category" class="text-xs text-[#5a8ab5]">{{ item.category }}</span>
          </div>

          <h2 class="text-xl font-semibold text-white">{{ item.name }}</h2>
          <p v-if="item.brand" class="text-[#96BEE6]">{{ item.brand }}</p>

          <div v-if="item.userRating">
            <StarRating :rating="item.userRating" size="md" />
          </div>

          <p v-if="item.userNotes" class="text-sm text-[#5a8ab5]">{{ item.userNotes }}</p>

          <div v-if="item.venue" class="text-sm text-[#5a8ab5]">
            Venue: {{ item.venue.name || 'Unknown' }}
          </div>

          <div v-if="item.tags?.length" class="flex flex-wrap gap-1">
            <span v-for="tag in item.tags" :key="tag"
              class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6]">
              {{ tag }}
            </span>
          </div>
        </div>
      </div>

      <!-- Save to wishlist -->
      <button
        @click="saveToWishlist"
        class="w-full bg-[#0a2a52] border border-[#1e407c]/50 hover:border-[#1e407c] text-[#96BEE6] py-3 rounded-xl font-medium mb-4 transition-colors"
      >
        Save to My Wishlist
      </button>

      <!-- Thoughts section -->
      <section class="space-y-4">
        <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">
          Thoughts ({{ thoughts.length }})
        </h3>

        <!-- Composer -->
        <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
          <textarea
            v-model="newThoughtContent"
            placeholder="Share your thoughts..."
            maxlength="500"
            rows="3"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c] resize-none"
          ></textarea>

          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <span class="text-xs text-[#5a8ab5]">Rate:</span>
              <button
                v-for="star in 5"
                :key="star"
                @click="newThoughtRating = newThoughtRating === star ? undefined : star"
                class="text-lg"
                :class="newThoughtRating && star <= newThoughtRating ? 'text-[#96BEE6]' : 'text-[#1e407c]/50'"
              >
                &#9733;
              </button>
            </div>

            <button
              @click="submitThought"
              :disabled="!newThoughtContent.trim() || isSubmitting"
              class="bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#0a2a52] disabled:text-[#4a7aa5]/60 text-white px-4 py-2 rounded-xl text-sm font-medium transition-colors"
            >
              {{ isSubmitting ? 'Posting...' : 'Post' }}
            </button>
          </div>
          <p class="text-xs text-[#4a7aa5] text-right">{{ newThoughtContent.length }}/500</p>
        </div>

        <!-- Thoughts list -->
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
          <div v-if="thought.rating" class="mb-1">
            <span v-for="star in 5" :key="star"
              :class="star <= thought.rating ? 'text-[#96BEE6]' : 'text-[#1e407c]/50'">&#9733;</span>
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
