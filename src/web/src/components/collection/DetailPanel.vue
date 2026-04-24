<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useItemsStore } from '../../stores/items'
import StarRating from '../common/StarRating.vue'

const props = defineProps<{
  itemId: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'navigate', id: string): void
}>()

const itemsStore = useItemsStore()

const item = computed(() =>
  itemsStore.items.find(i => i.id === props.itemId) ??
  itemsStore.wishlistItems.find(i => i.id === props.itemId)
)

const activePhoto = ref(0)

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') emit('close')
}

onMounted(() => document.addEventListener('keydown', onKeydown))
onUnmounted(() => document.removeEventListener('keydown', onKeydown))

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}
</script>

<template>
  <Transition name="slide">
    <aside
      v-if="item"
      class="w-96 shrink-0 bg-[#041e3e] border-l border-[#0a2a52] overflow-y-auto"
    >
      <!-- Header -->
      <div class="flex items-center justify-between p-4 border-b border-[#0a2a52]">
        <h2 class="text-sm font-semibold text-white truncate">Details</h2>
        <div class="flex items-center gap-2">
          <button
            @click="emit('navigate', item!.id)"
            class="text-xs text-[#96BEE6] hover:text-white transition-colors"
          >
            Full Page
          </button>
          <button
            @click="emit('close')"
            class="w-8 h-8 flex items-center justify-center rounded-lg text-[#96BEE6] hover:bg-[#0a2a52] hover:text-white transition-colors"
            aria-label="Close detail panel"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      </div>

      <!-- Photos -->
      <div v-if="item.photoUrls.length" class="p-4">
        <img
          :src="item.photoUrls[activePhoto] ?? item.photoUrls[0]"
          class="w-full h-56 object-cover rounded-lg"
        />
        <div v-if="item.photoUrls.length > 1" class="flex gap-2 mt-2">
          <button
            v-for="(url, i) in item.photoUrls"
            :key="i"
            @click="activePhoto = i"
            class="w-12 h-12 rounded-md overflow-hidden border-2 transition-colors"
            :class="activePhoto === i ? 'border-[#96BEE6]' : 'border-[#0a2a52]'"
          >
            <img :src="url" class="w-full h-full object-cover" />
          </button>
        </div>
      </div>
      <div v-else class="p-4">
        <div class="w-full h-56 bg-[#0a2a52] rounded-lg flex items-center justify-center text-[#96BEE6]/70 uppercase">
          {{ item.type }}
        </div>
      </div>

      <!-- Info -->
      <div class="px-4 pb-4 space-y-4">
        <div>
          <span class="inline-block text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6] mb-2">
            {{ item.type }}
          </span>
          <h3 class="text-lg font-semibold text-white">{{ item.name }}</h3>
          <p v-if="item.brand" class="text-sm text-[#96BEE6]/70">{{ item.brand }}</p>
        </div>

        <div v-if="item.userRating">
          <label class="block text-xs text-[#96BEE6]/60 mb-1">Rating</label>
          <StarRating :rating="item.userRating" size="md" />
        </div>

        <div v-if="item.userNotes">
          <label class="block text-xs text-[#96BEE6]/60 mb-1">Notes</label>
          <p class="text-sm text-white/90 whitespace-pre-wrap">{{ item.userNotes }}</p>
        </div>

        <div v-if="item.aiSummary">
          <label class="block text-xs text-[#96BEE6]/60 mb-1">AI Summary</label>
          <p class="text-sm text-white/80">{{ item.aiSummary }}</p>
        </div>

        <div v-if="item.tags.length">
          <label class="block text-xs text-[#96BEE6]/60 mb-1">Tags</label>
          <div class="flex flex-wrap gap-1">
            <span
              v-for="tag in item.tags"
              :key="tag"
              class="text-xs px-2 py-0.5 rounded-full bg-[#0a2a52] text-[#96BEE6]"
            >
              {{ tag }}
            </span>
          </div>
        </div>

        <div v-if="item.venue">
          <label class="block text-xs text-[#96BEE6]/60 mb-1">Venue</label>
          <p class="text-sm text-white/90">{{ item.venue.name }}</p>
          <p v-if="item.venue.address" class="text-xs text-[#96BEE6]/50">{{ item.venue.address }}</p>
        </div>

        <div class="flex gap-4 text-xs text-[#4a7aa5]/60 pt-2 border-t border-[#0a2a52]">
          <span>Added {{ formatDate(item.createdAt) }}</span>
          <span>Updated {{ formatDate(item.updatedAt) }}</span>
        </div>
      </div>
    </aside>
  </Transition>
</template>

<style scoped>
.slide-enter-active,
.slide-leave-active {
  transition: transform 0.2s ease;
}
.slide-enter-from,
.slide-leave-to {
  transform: translateX(100%);
}
</style>
