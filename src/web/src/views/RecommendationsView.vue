<script setup lang="ts">
import { onMounted } from 'vue'
import { useCamera } from '../composables/useCamera'
import { useBreakpoint } from '../composables/useBreakpoint'
import { useRecommendationsStore } from '../stores/recommendations'
import { storeToRefs } from 'pinia'

const { isDesktop } = useBreakpoint()
const { photos, previews, addFromInput, removePhoto, clearPhotos } = useCamera()
const store = useRecommendationsStore()

const {
  preferences,
  isLoadingRecommendations,
  isLoadingProfile,
  isUploadingPhoto,
  isError,
  recommendations,
  reasoning,
  basedOnItems,
  extractedMenuItems,
  profileData,
  selectedTypes,
  hasRecommendations,
  savedItems,
  savingItems,
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
  const photo = photos.value.length > 0 ? photos.value[0] : undefined
  await store.getRecommendations(photo)
}

function handleReset() {
  clearPhotos()
  store.reset()
}

onMounted(() => {
  if (!store.profileData) {
    store.loadUserProfile()
  }
})
</script>

<template>
  <div class="p-4 mx-auto pb-24" :class="isDesktop ? 'max-w-6xl' : 'max-w-lg'">
    <div class="flex items-center justify-between mb-2">
      <h1 class="text-2xl font-bold text-white">AI Recommendations</h1>
      <router-link
        v-if="!isDesktop"
        to="/search"
        class="text-[#96BEE6]/70 hover:text-[#96BEE6] transition-colors"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
      </router-link>
    </div>
    <div class="mb-6">
      <p class="text-[#96BEE6] text-sm">
        Get personalized recommendations based on your ratings
      </p>
    </div>

    <!-- User Profile Summary -->
    <div v-if="profileData && profileData.totalRatedItems > 0" class="mb-6 bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl p-4">
      <h2 class="text-[#96BEE6] text-sm font-medium mb-2">Your Profile</h2>
      <div class="space-y-1 text-sm">
        <p class="text-white/80">
          <span class="text-[#4a7aa5]">Rated items:</span> {{ profileData.totalRatedItems }}
        </p>
        <p class="text-white/80">
          <span class="text-[#4a7aa5]">Average rating:</span> {{ profileData.averageRating.toFixed(1) }}/5.0
        </p>
        <p v-if="profileData.topRatedItems.length > 0" class="text-white/80">
          <span class="text-[#4a7aa5]">Top favorites:</span> {{ profileData.topRatedItems.slice(0, 3).map((i) => i.name).join(', ') }}
        </p>
      </div>
    </div>

    <div v-else-if="!isLoadingProfile" class="mb-6 bg-yellow-900/30 border border-yellow-700/50 text-yellow-300 px-4 py-3 rounded-xl text-sm">
      Rate some items first to get personalized recommendations.
    </div>

    <!-- Menu Photo Upload (Optional) -->
    <div class="mb-6">
      <label class="block text-sm text-[#96BEE6] mb-2">
        Menu Photo <span class="text-[#4a7aa5]/60">(optional)</span>
      </label>
      <p class="text-xs text-[#4a7aa5] mb-3">
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
        <label class="flex-1 bg-[#0a2a52] hover:bg-[#1e407c] border border-[#1e407c]/50 rounded-xl py-3 flex flex-col items-center cursor-pointer transition-colors">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-1 text-white/80" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
            <circle cx="12" cy="13" r="3" />
          </svg>
          <span class="text-xs text-[#96BEE6]">Camera</span>
          <input type="file" accept="image/*" capture="environment" @change="addFromInput" class="hidden" />
        </label>

        <label class="flex-1 bg-[#0a2a52] hover:bg-[#1e407c] border border-[#1e407c]/50 rounded-xl py-3 flex flex-col items-center cursor-pointer transition-colors">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-1 text-white/80" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
          </svg>
          <span class="text-xs text-[#96BEE6]">Gallery</span>
          <input type="file" accept="image/*" @change="addFromInput" class="hidden" />
        </label>
      </div>
    </div>

    <!-- Preferences -->
    <div class="mb-6">
      <label class="block text-sm text-[#96BEE6] mb-2">
        Additional Preferences <span class="text-[#4a7aa5]/60">(optional)</span>
      </label>
      <textarea
        v-model="preferences"
        placeholder="E.g., 'I prefer smoky whiskeys' or 'Looking for something sweet'"
        rows="3"
        class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c] resize-none"
      />
    </div>

    <!-- Item Type Filter -->
    <div class="mb-6">
      <label class="block text-sm text-[#96BEE6] mb-2">
        Focus on Types <span class="text-[#4a7aa5]/60">(optional)</span>
      </label>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="type in availableTypes"
          :key="type"
          @click="store.toggleType(type)"
          :class="[
            'px-3 py-1 rounded-full text-xs border transition-colors',
            selectedTypes.includes(type)
              ? 'bg-[#1e407c] border-[#96BEE6] text-white'
              : 'bg-[#0a2a52] border-[#1e407c]/50 text-[#96BEE6] hover:bg-[#1e407c]'
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
      class="w-full bg-[#1e407c] hover:bg-[#2d5596] text-white font-medium py-4 rounded-xl disabled:opacity-50 disabled:cursor-not-allowed transition-colors mb-6"
    >
      <span v-if="isUploadingPhoto">Uploading photo...</span>
      <span v-else-if="isLoadingRecommendations">Generating recommendations...</span>
      <span v-else>Get Recommendations</span>
    </button>

    <!-- Extracted Menu Items -->
    <div v-if="extractedMenuItems.length > 0" class="mb-6 bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl p-4">
      <h3 class="text-[#96BEE6] text-sm font-medium mb-2">Menu Items Found</h3>
      <ul class="space-y-1">
        <li v-for="(item, index) in extractedMenuItems" :key="index" class="text-sm text-white/80">
          • {{ item }}
        </li>
      </ul>
    </div>

    <!-- Recommendations Results -->
    <div v-if="hasRecommendations" class="space-y-4">
      <div class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl p-4">
        <h2 class="text-[#96BEE6] font-medium mb-2">Recommendations</h2>
        <p v-if="reasoning" class="text-sm text-white/70 mb-4">{{ reasoning }}</p>

        <div v-if="basedOnItems.length > 0" class="text-xs text-[#4a7aa5] mb-4">
          Based on: {{ basedOnItems.join(', ') }}
        </div>
      </div>

      <div
        v-for="(rec, index) in recommendations"
        :key="index"
        class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl p-4 hover:border-[#96BEE6]/30 transition-colors"
      >
        <div class="flex items-start justify-between mb-2">
          <div class="flex-1">
            <h3 class="text-white font-medium">{{ rec.name }}</h3>
            <p class="text-sm text-[#96BEE6]">{{ rec.type }}</p>
            <p v-if="rec.brand" class="text-sm text-[#4a7aa5]">{{ rec.brand }}</p>
            <p v-if="rec.category" class="text-sm text-[#4a7aa5]">{{ rec.category }}</p>
          </div>
          <div class="flex flex-col items-end gap-1">
            <span class="text-xs bg-[#1e407c] text-white px-2 py-1 rounded">
              {{ (rec.confidence * 100).toFixed(0) }}% match
            </span>
            <span v-if="rec.matchedFromMenu" class="text-xs bg-green-900/50 text-green-300 px-2 py-1 rounded">
              On menu
            </span>
            <button
              v-if="!savedItems.has(index)"
              :disabled="savingItems.has(index)"
              @click="store.saveToWishlist(rec, index)"
              class="text-xs bg-amber-700/50 hover:bg-amber-700/80 text-amber-200 px-2 py-1 rounded transition-colors disabled:opacity-50"
            >
              <span v-if="savingItems.has(index)">Saving...</span>
              <span v-else>+ Wishlist</span>
            </button>
            <span v-else class="text-xs bg-green-800/50 text-green-300 px-2 py-1 rounded">
              Saved
            </span>
          </div>
        </div>
        <p class="text-sm text-white/70 mt-2">{{ rec.reason }}</p>
      </div>

      <button
        @click="handleReset"
        class="w-full bg-[#0a2a52] hover:bg-[#1e407c] border border-[#1e407c]/50 text-[#96BEE6] font-medium py-3 rounded-xl transition-colors"
      >
        Start Over
      </button>
    </div>

    <!-- No recommendations yet / Error state -->
    <div
      v-else-if="reasoning && !isLoadingRecommendations"
      class="rounded-xl p-4"
      :class="isError
        ? 'bg-red-900/30 border border-red-700/50 text-red-300'
        : 'bg-[#0a2a52] border border-[#1e407c]/50'"
    >
      <p :class="isError ? 'text-red-300 text-sm' : 'text-white/70 text-sm'">{{ reasoning }}</p>
    </div>
  </div>
</template>
