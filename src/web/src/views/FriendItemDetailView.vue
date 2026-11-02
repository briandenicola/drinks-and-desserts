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
const isAdding = ref(false)
const addSuccess = ref(false)

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

async function addToMyCollection() {
  if (!item.value || isAdding.value) return
  isAdding.value = true
  error.value = ''
  try {
    await friendsApi.addFriendItem(friendId.value, itemId.value)
    addSuccess.value = true
  } catch {
    error.value = 'Failed to add to your collection'
  } finally {
    isAdding.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <button @click="router.back()" class="text-th-accent text-sm mb-4">&larr; Back</button>

    <div v-if="isLoading" class="text-center text-th-muted-strong py-12">Loading...</div>

    <div v-else-if="error && !item" class="bg-th-danger/15 border border-th-danger/40 text-th-danger-soft rounded-xl p-3 text-sm">
      {{ error }}
    </div>

    <template v-else-if="item">
      <!-- Item detail -->
      <div class="bg-th-inset border border-th-line rounded-xl overflow-hidden mb-4">
        <img
          v-if="item.photoUrls?.length"
          :src="item.photoUrls[0]"
          class="w-full h-48 object-cover"
        />

        <div class="p-4 space-y-3">
          <div class="flex items-center gap-2">
            <span class="text-xs px-2 py-0.5 rounded-full bg-th-card text-th-accent">{{ item.type }}</span>
            <span v-if="item.category" class="text-xs text-th-muted-strong">{{ item.category }}</span>
          </div>

          <h2 class="text-xl font-semibold text-th-heading">{{ item.name }}</h2>
          <p v-if="item.brand" class="text-th-accent">{{ item.brand }}</p>

          <div v-if="item.userRating">
            <StarRating :rating="item.userRating" size="md" />
          </div>

          <p v-if="item.userNotes" class="text-sm text-th-muted-strong">{{ item.userNotes }}</p>

          <div v-if="item.venue" class="text-sm text-th-muted-strong">
            Venue: {{ item.venue.name || 'Unknown' }}
          </div>

          <div v-if="item.tags?.length" class="flex flex-wrap gap-1">
            <span v-for="tag in item.tags" :key="tag"
              class="text-xs px-2 py-0.5 rounded-full bg-th-card text-th-accent">
              {{ tag }}
            </span>
          </div>
        </div>
      </div>

      <!-- Add to my collection -->
      <button
        v-if="!addSuccess"
        @click="addToMyCollection"
        :disabled="isAdding"
        class="w-full min-h-[44px] bg-th-card border border-th-edge/50 hover:border-th-edge disabled:opacity-60 text-th-accent py-3 rounded-xl font-medium mb-4 transition-colors"
      >
        {{ isAdding ? 'Adding...' : 'Add to My Collection' }}
      </button>
      <div v-else class="w-full min-h-[44px] bg-th-card border border-th-edge/50 text-th-accent py-3 rounded-xl font-medium mb-4 text-center">
        Added to your collection
      </div>

      <!-- Thoughts section -->
      <section class="space-y-4">
        <h3 class="text-sm font-medium text-th-emphasis uppercase tracking-wide">
          Thoughts ({{ thoughts.length }})
        </h3>

        <!-- Composer -->
        <div class="bg-th-inset border border-th-line rounded-xl p-4 space-y-3">
          <textarea
            v-model="newThoughtContent"
            placeholder="Share your thoughts..."
            maxlength="500"
            rows="3"
            class="w-full bg-th-card border border-th-edge/50 rounded-xl px-4 py-3 text-th-text placeholder-th-muted focus:outline-none focus:border-th-edge resize-none"
          ></textarea>

          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <span class="text-xs text-th-muted-strong">Rate:</span>
              <button
                v-for="star in 5"
                :key="star"
                @click="newThoughtRating = newThoughtRating === star ? undefined : star"
                class="text-lg"
                :class="newThoughtRating && star <= newThoughtRating ? 'text-th-accent' : 'text-th-edge/50'"
              >
                &#9733;
              </button>
            </div>

            <button
              @click="submitThought"
              :disabled="!newThoughtContent.trim() || isSubmitting"
              class="bg-th-brand hover:bg-th-brand-hover disabled:bg-th-card disabled:text-th-muted text-white px-4 py-2 rounded-xl text-sm font-medium transition-colors"
            >
              {{ isSubmitting ? 'Posting...' : 'Post' }}
            </button>
          </div>
          <p class="text-xs text-th-muted text-right">{{ newThoughtContent.length }}/500</p>
        </div>

        <!-- Thoughts list -->
        <div v-for="thought in thoughts" :key="thought.id"
          class="bg-th-inset border border-th-line rounded-xl p-4">
          <div class="flex items-start justify-between mb-2">
            <div>
              <span class="font-medium text-th-text text-sm">{{ thought.authorDisplayName }}</span>
              <span class="text-xs text-th-muted ml-2">{{ new Date(thought.createdAt).toLocaleDateString() }}</span>
            </div>
            <button
              v-if="thought.authorId === auth.user?.id"
              @click="deleteThought(thought)"
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

        <div v-if="thoughts.length === 0" class="text-center text-th-muted-strong py-4 text-sm">
          No thoughts yet. Be the first to share one.
        </div>
      </section>

      <div v-if="error" class="bg-th-danger/15 border border-th-danger/40 text-th-danger-soft rounded-xl p-3 text-sm mt-4">
        {{ error }}
      </div>
    </template>
  </div>
</template>
