<script setup lang="ts">
import { onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useCamera } from '../composables/useCamera'
import { useBreakpoint } from '../composables/useBreakpoint'
import { useRecommendationsStore } from '../stores/recommendations'

const { isDesktop } = useBreakpoint()
const { photos, previews, addFromInput, removePhoto, clearPhotos } = useCamera()

const store = useRecommendationsStore()
const {
  profileData,
  isLoadingProfile,
  preferences,
  selectedTypes,
  recommendations,
  reasoning,
  basedOnItems,
  extractedMenuItems,
  isLoadingRecommendations,
  isUploadingPhoto,
  isError,
  savedItems,
  savingItems,
  isSavingThread,
  savedThreadId,
  hasRecommendations,
} = storeToRefs(store)

const availableTypes = [
  'whiskey',
  'wine',
  'cocktail',
  'vodka',
  'gin',
  'cigar',
  'dessert',
  'coffee',
  'espresso',
  'latte',
  'cappuccino',
  'cold-brew',
  'pour-over',
]

async function handleGetRecommendations() {
  const photoToUpload = photos.value.length > 0 ? photos.value[0] : undefined
  await store.getRecommendations(photoToUpload)
}

function handleReset() {
  clearPhotos()
  store.reset()
}

onMounted(() => {
  // Only load profile if not already loaded
  if (!profileData.value) {
    store.loadUserProfile()
  }
})
</script>

<template>
  <div class="p-4 mx-auto pb-24" :class="isDesktop ? 'max-w-6xl' : 'max-w-lg'">
    <div class="flex items-center justify-between mb-2">
      <h1 class="text-2xl font-bold text-th-heading">AI Recommendations</h1>
      <div class="flex items-center gap-4">
        <router-link
          to="/recommendations/history"
          class="text-th-accent/70 hover:text-th-accent transition-colors"
          title="Past recommendations"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
        </router-link>
        <router-link
          v-if="!isDesktop"
          to="/search"
          class="text-th-accent/70 hover:text-th-accent transition-colors"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </router-link>
      </div>
    </div>
    <div class="mb-6">
      <p class="text-th-accent text-sm">
        Get personalized recommendations based on your ratings
      </p>
    </div>

    <!-- User Profile Summary -->
    <div v-if="profileData && profileData.totalRatedItems > 0" class="mb-6 bg-th-card border border-th-edge/50 rounded-xl p-4">
      <h2 class="text-th-emphasis text-sm font-medium mb-2">Your Profile</h2>
      <div class="space-y-1 text-sm">
        <p class="text-th-text/80">
          <span class="text-th-muted">Rated items:</span> {{ profileData.totalRatedItems }}
        </p>
        <p class="text-th-text/80">
          <span class="text-th-muted">Average rating:</span> {{ profileData.averageRating.toFixed(1) }}/5.0
        </p>
        <p v-if="profileData.topRatedItems.length > 0" class="text-th-text/80">
          <span class="text-th-muted">Top favorites:</span> {{ profileData.topRatedItems.slice(0, 3).map((i) => i.name).join(', ') }}
        </p>
      </div>
    </div>

    <div v-else-if="!isLoadingProfile" class="mb-6 bg-th-warning/15 border border-th-warning/40 text-th-warning px-4 py-3 rounded-xl text-sm">
      Rate some items first to get personalized recommendations.
    </div>

    <!-- Menu Photo Upload (Optional) -->
    <div class="mb-6">
      <label class="block text-sm text-th-accent mb-2">
        Menu Photo <span class="text-th-muted/60">(optional)</span>
      </label>
      <p class="text-xs text-th-muted mb-3">
        Upload a photo of a menu to get recommendations from specific items
      </p>

      <!-- Photo preview -->
      <div v-if="previews.length" class="mb-3">
        <div class="relative inline-block">
          <img :src="previews[0]" class="w-full max-w-xs rounded-lg" />
          <button
            @click="removePhoto(0)"
            class="absolute -top-2 -right-2 bg-red-600 text-white rounded-full w-6 h-6 flex items-center justify-center text-sm"
          >×</button>
        </div>
      </div>

      <!-- Camera / Gallery buttons -->
      <div v-if="!previews.length" class="flex gap-3">
        <label class="group flex-1 bg-th-card hover:bg-th-brand border border-th-edge/50 rounded-xl py-3 flex flex-col items-center cursor-pointer transition-colors">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-1 text-th-text/80 group-hover:text-th-on-brand" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
            <circle cx="12" cy="13" r="3" />
          </svg>
          <span class="text-xs text-th-accent group-hover:text-th-on-brand">Camera</span>
          <input type="file" accept="image/*" capture="environment" @change="addFromInput" class="hidden" />
        </label>

        <label class="group flex-1 bg-th-card hover:bg-th-brand border border-th-edge/50 rounded-xl py-3 flex flex-col items-center cursor-pointer transition-colors">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-1 text-th-text/80 group-hover:text-th-on-brand" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
          </svg>
          <span class="text-xs text-th-accent group-hover:text-th-on-brand">Gallery</span>
          <input type="file" accept="image/*" @change="addFromInput" class="hidden" />
        </label>
      </div>
    </div>

    <!-- Preferences -->
    <div class="mb-6">
      <label class="block text-sm text-th-accent mb-2">
        Additional Preferences <span class="text-th-muted/60">(optional)</span>
      </label>
      <textarea
        v-model="preferences"
        placeholder="E.g., 'I prefer smoky whiskeys' or 'Looking for something sweet'"
        rows="3"
        class="w-full bg-th-card border border-th-edge/50 rounded-xl px-4 py-3 text-th-text placeholder-th-muted focus:outline-none focus:border-th-edge resize-none"
      />
    </div>

    <!-- Item Type Filter -->
    <div class="mb-6">
      <label class="block text-sm text-th-accent mb-2">
        Focus on Types <span class="text-th-muted/60">(optional)</span>
      </label>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="type in availableTypes"
          :key="type"
          @click="store.toggleType(type)"
          :class="[
            'px-3 py-1 rounded-full text-xs border transition-colors',
            selectedTypes.includes(type)
              ? 'bg-th-brand border-th-emphasis text-th-on-brand'
              : 'bg-th-card border-th-edge/50 text-th-accent hover:bg-th-brand hover:text-th-on-brand'
          ]"
        >
          {{ type }}
        </button>
      </div>
    </div>

    <!-- Get Recommendations Button -->
    <button
      @click="handleGetRecommendations"
      :disabled="isLoadingRecommendations || isUploadingPhoto || (profileData !== null && profileData.totalRatedItems === 0)"
      class="w-full bg-th-brand hover:bg-th-brand-hover text-th-on-brand font-medium py-4 rounded-xl disabled:opacity-50 disabled:cursor-not-allowed transition-colors mb-6"
    >
      <span v-if="isUploadingPhoto">Uploading photo...</span>
      <span v-else-if="isLoadingRecommendations">Generating recommendations...</span>
      <span v-else>Get Recommendations</span>
    </button>

    <!-- Extracted Menu Items -->
    <div v-if="extractedMenuItems.length > 0" class="mb-6 bg-th-card border border-th-edge/50 rounded-xl p-4">
      <h3 class="text-th-emphasis text-sm font-medium mb-2">Menu Items Found</h3>
      <ul class="space-y-1">
        <li v-for="(item, index) in extractedMenuItems" :key="index" class="text-sm text-th-text/80">
          • {{ item }}
        </li>
      </ul>
    </div>

    <!-- Recommendations Results -->
    <div v-if="hasRecommendations" class="space-y-4">
      <div class="bg-th-card border border-th-edge/50 rounded-xl p-4">
        <h2 class="text-th-emphasis font-medium mb-2">Recommendations</h2>
        <p v-if="reasoning" class="text-sm text-th-text/70 mb-4">{{ reasoning }}</p>

        <div v-if="basedOnItems.length > 0" class="text-xs text-th-muted mb-4">
          Based on: {{ basedOnItems.join(', ') }}
        </div>

        <button
          v-if="!savedThreadId"
          :disabled="isSavingThread"
          @click="store.saveThread()"
          class="text-xs bg-th-brand hover:bg-th-brand-hover text-th-on-brand px-3 py-1.5 rounded transition-colors disabled:opacity-50"
        >
          <span v-if="isSavingThread">Saving...</span>
          <span v-else>Save this recommendation</span>
        </button>
        <span v-else class="text-xs bg-th-success/15 text-th-success-soft px-3 py-1.5 rounded">
          Saved to history
        </span>
      </div>

      <div
        v-for="(rec, index) in recommendations"
        :key="index"
        class="bg-th-card border border-th-edge/50 rounded-xl p-4 hover:border-th-emphasis/30 transition-colors"
      >
        <div class="flex items-start justify-between mb-2">
          <div class="flex-1">
            <h3 class="text-th-heading font-medium">{{ rec.name }}</h3>
            <p class="text-sm text-th-accent">{{ rec.type }}</p>
            <p v-if="rec.brand" class="text-sm text-th-muted">{{ rec.brand }}</p>
            <p v-if="rec.category" class="text-sm text-th-muted">{{ rec.category }}</p>
          </div>
          <div class="flex flex-col items-end gap-1">
            <span class="text-xs bg-th-brand text-th-on-brand px-2 py-1 rounded">
              {{ (rec.confidence * 100).toFixed(0) }}% match
            </span>
            <span v-if="rec.matchedFromMenu" class="text-xs bg-th-success/15 text-th-success-soft px-2 py-1 rounded">
              On menu
            </span>
            <button
              v-if="!savedItems.has(index)"
              :disabled="savingItems.has(index)"
              @click="store.saveToWishlist(rec, index)"
              class="text-xs bg-th-warning/15 hover:bg-th-warning/25 text-th-warning px-2 py-1 rounded transition-colors disabled:opacity-50"
            >
              <span v-if="savingItems.has(index)">Saving...</span>
              <span v-else>+ Wishlist</span>
            </button>
            <span v-else class="text-xs bg-th-success/15 text-th-success-soft px-2 py-1 rounded">
              Saved
            </span>
          </div>
        </div>
        <p class="text-sm text-th-text/70 mt-2">{{ rec.reason }}</p>
      </div>

      <button
        @click="handleReset"
        class="w-full bg-th-card hover:bg-th-brand hover:text-th-on-brand border border-th-edge/50 text-th-accent font-medium py-3 rounded-xl transition-colors"
      >
        Start Over
      </button>
    </div>

    <!-- No recommendations yet / Error state -->
    <div
      v-else-if="reasoning && !isLoadingRecommendations"
      class="rounded-xl p-4"
      :class="isError
        ? 'bg-th-danger/15 border border-th-danger/40 text-th-danger-soft'
        : 'bg-th-card border border-th-edge/50'"
    >
      <p :class="isError ? 'text-th-danger-soft text-sm' : 'text-th-text/70 text-sm'">{{ reasoning }}</p>
    </div>
  </div>
</template>
