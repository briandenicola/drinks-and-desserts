<script setup lang="ts">
import { ref, inject, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { usersApi } from '../services/users'
import { RefreshKey } from '../composables/refreshKey'
import StarRating from '../components/common/StarRating.vue'

const router = useRouter()
const registerRefresh = inject(RefreshKey)

interface TopItem {
  id: string
  name: string
  type: string
  brand: string | null
  rating: number
  photoUrl: string | null
}

interface Stats {
  overview: {
    totalItems: number
    totalCaptures: number
    totalRated: number
    uniqueBrands: number
    uniqueVenues: number
    memberSince: string
  }
  typeBreakdown: { type: string; count: number }[]
  avgRatingByType: { type: string; avgRating: number; count: number }[]
  topRated: TopItem[]
  topVenues: { name: string; count: number }[]
  topTags: { tag: string; count: number }[]
  activityByDay: { day: string; count: number }[]
  monthlyTrend: { month: string; count: number }[]
  ratingTrend: { month: string; avgRating: number; count: number }[]
}

const stats = ref<Stats | null>(null)
const isLoading = ref(true)
const error = ref('')

async function loadStats() {
  isLoading.value = true
  error.value = ''
  try {
    const { data } = await usersApi.getStats()
    stats.value = data as Stats
  } catch {
    error.value = 'Failed to load stats'
  } finally {
    isLoading.value = false
  }
}

registerRefresh?.(loadStats)
onMounted(loadStats)

const memberDuration = computed(() => {
  if (!stats.value) return ''
  const since = new Date(stats.value.overview.memberSince)
  const now = new Date()
  const days = Math.floor((now.getTime() - since.getTime()) / 86400000)
  if (days < 30) return `${days} day${days !== 1 ? 's' : ''}`
  const months = Math.floor(days / 30)
  if (months < 12) return `${months} month${months !== 1 ? 's' : ''}`
  const years = Math.floor(months / 12)
  const rem = months % 12
  return rem > 0 ? `${years}y ${rem}m` : `${years} year${years !== 1 ? 's' : ''}`
})

const maxActivity = computed(() => {
  if (!stats.value) return 1
  return Math.max(1, ...stats.value.activityByDay.map(d => d.count))
})

const maxMonthly = computed(() => {
  if (!stats.value) return 1
  return Math.max(1, ...stats.value.monthlyTrend.map(m => m.count))
})

const typeColors: Record<string, string> = {
  whiskey: 'bg-amber-600',
  wine: 'bg-red-700',
  cocktail: 'bg-sky-600',
  cigar: 'bg-stone-600',
  dessert: 'bg-pink-600',
  vodka: 'bg-blue-500',
  gin: 'bg-emerald-600',
}

const typeTextColors: Record<string, string> = {
  whiskey: 'text-amber-500',
  wine: 'text-red-400',
  cocktail: 'text-sky-400',
  cigar: 'text-stone-400',
  dessert: 'text-pink-400',
  vodka: 'text-blue-400',
  gin: 'text-emerald-400',
}

function typeColor(type: string) {
  return typeColors[type] || 'bg-stone-600'
}

function typeTextColor(type: string) {
  return typeTextColors[type] || 'text-stone-400'
}
</script>

<template>
  <div class="p-4 max-w-lg mx-auto space-y-6">
    <h2 class="text-xl font-semibold">Stats</h2>

    <div v-if="isLoading" class="text-center py-12 text-stone-500">Loading stats...</div>
    <div v-else-if="error" class="text-center py-12 text-red-400">{{ error }}</div>

    <template v-else-if="stats">
      <!-- Overview Cards -->
      <div class="grid grid-cols-2 gap-3">
        <div class="bg-stone-900 border border-stone-800 rounded-xl p-4 text-center">
          <div class="text-2xl font-bold text-amber-500">{{ stats.overview.totalItems }}</div>
          <div class="text-xs text-stone-500 mt-1">Items</div>
        </div>
        <div class="bg-stone-900 border border-stone-800 rounded-xl p-4 text-center">
          <div class="text-2xl font-bold text-amber-500">{{ stats.overview.totalCaptures }}</div>
          <div class="text-xs text-stone-500 mt-1">Captures</div>
        </div>
        <div class="bg-stone-900 border border-stone-800 rounded-xl p-4 text-center">
          <div class="text-2xl font-bold text-amber-500">{{ stats.overview.uniqueBrands }}</div>
          <div class="text-xs text-stone-500 mt-1">Brands</div>
        </div>
        <div class="bg-stone-900 border border-stone-800 rounded-xl p-4 text-center">
          <div class="text-2xl font-bold text-amber-500">{{ memberDuration }}</div>
          <div class="text-xs text-stone-500 mt-1">Member</div>
        </div>
      </div>

      <!-- Type Breakdown -->
      <section v-if="stats.typeBreakdown.length" class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-3">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">By Type</h3>
        <div v-for="t in stats.typeBreakdown" :key="t.type" class="space-y-1">
          <div class="flex justify-between text-sm">
            <span class="capitalize" :class="typeTextColor(t.type)">{{ t.type }}</span>
            <span class="text-stone-500">{{ t.count }}</span>
          </div>
          <div class="h-2 bg-stone-800 rounded-full overflow-hidden">
            <div
              class="h-full rounded-full transition-all duration-500"
              :class="typeColor(t.type)"
              :style="{ width: (t.count / stats!.overview.totalItems * 100) + '%' }"
            />
          </div>
        </div>
      </section>

      <!-- Average Rating by Type -->
      <section v-if="stats.avgRatingByType.length" class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-3">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">Average Rating</h3>
        <div v-for="t in stats.avgRatingByType" :key="t.type" class="flex items-center justify-between">
          <span class="capitalize text-sm" :class="typeTextColor(t.type)">{{ t.type }}</span>
          <div class="flex items-center gap-2">
            <StarRating :rating="t.avgRating" size="sm" />
            <span class="text-xs text-stone-500">{{ t.avgRating }} ({{ t.count }})</span>
          </div>
        </div>
      </section>

      <!-- Top Rated -->
      <section v-if="stats.topRated.length" class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-3">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">Top Rated</h3>
        <div
          v-for="(item, idx) in stats.topRated"
          :key="item.id"
          @click="router.push(`/items/${item.id}`)"
          class="flex items-center gap-3 cursor-pointer hover:bg-stone-800/50 rounded-lg p-2 -mx-2 transition-colors"
        >
          <span class="text-lg font-bold text-stone-600 w-6 text-center">{{ idx + 1 }}</span>
          <img
            v-if="item.photoUrl"
            :src="item.photoUrl"
            class="w-10 h-10 rounded-lg object-cover"
            alt=""
          />
          <div v-else class="w-10 h-10 rounded-lg bg-stone-800 flex items-center justify-center">
            <span class="text-xs text-stone-600">N/A</span>
          </div>
          <div class="flex-1 min-w-0">
            <div class="text-sm text-stone-200 truncate">{{ item.name }}</div>
            <div class="text-xs text-stone-500 capitalize">{{ item.brand || item.type }}</div>
          </div>
          <StarRating :rating="item.rating" size="sm" />
        </div>
      </section>

      <!-- Favorite Venues -->
      <section v-if="stats.topVenues.length" class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-3">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">Favorite Venues</h3>
        <div v-for="v in stats.topVenues" :key="v.name" class="flex justify-between items-center">
          <span class="text-sm text-stone-300 truncate">{{ v.name }}</span>
          <span class="text-xs text-stone-500 ml-2 shrink-0">{{ v.count }} visit{{ v.count !== 1 ? 's' : '' }}</span>
        </div>
      </section>

      <!-- Top Tags -->
      <section v-if="stats.topTags.length" class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-3">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">Top Tags</h3>
        <div class="flex flex-wrap gap-2">
          <span
            v-for="t in stats.topTags"
            :key="t.tag"
            class="px-2.5 py-1 rounded-full text-xs border border-stone-700 text-stone-400"
          >
            {{ t.tag }} <span class="text-stone-600">({{ t.count }})</span>
          </span>
        </div>
      </section>

      <!-- Activity by Day -->
      <section class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-3">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">Activity by Day</h3>
        <div class="flex items-end justify-between gap-1 h-24">
          <div v-for="d in stats.activityByDay" :key="d.day" class="flex-1 flex flex-col items-center gap-1">
            <div
              class="w-full bg-amber-600/80 rounded-t transition-all duration-500"
              :style="{ height: (d.count / maxActivity * 100) + '%', minHeight: d.count > 0 ? '4px' : '0' }"
            />
            <span class="text-[10px] text-stone-600">{{ d.day }}</span>
          </div>
        </div>
        <div class="text-center text-xs text-stone-600">captures per day of week</div>
      </section>

      <!-- Monthly Trend -->
      <section v-if="stats.monthlyTrend.some(m => m.count > 0)" class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-3">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">Monthly Trend</h3>
        <div class="flex items-end justify-between gap-1 h-24">
          <div v-for="m in stats.monthlyTrend" :key="m.month" class="flex-1 flex flex-col items-center gap-1">
            <span v-if="m.count > 0" class="text-[10px] text-stone-500">{{ m.count }}</span>
            <div
              class="w-full bg-amber-600/80 rounded-t transition-all duration-500"
              :style="{ height: (m.count / maxMonthly * 100) + '%', minHeight: m.count > 0 ? '4px' : '0' }"
            />
            <span class="text-[10px] text-stone-600">{{ m.month.split(' ')[0] }}</span>
          </div>
        </div>
        <div class="text-center text-xs text-stone-600">items added per month</div>
      </section>

      <!-- Rating Trend -->
      <section v-if="stats.ratingTrend.some(m => m.count > 0)" class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-3">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">Rating Trend</h3>
        <div class="flex items-end justify-between gap-1 h-24">
          <div v-for="m in stats.ratingTrend" :key="m.month" class="flex-1 flex flex-col items-center gap-1">
            <span v-if="m.count > 0" class="text-[10px] text-stone-500">{{ m.avgRating }}</span>
            <div
              class="w-full bg-amber-500/70 rounded-t transition-all duration-500"
              :style="{ height: m.count > 0 ? (m.avgRating / 5 * 100) + '%' : '0', minHeight: m.count > 0 ? '4px' : '0' }"
            />
            <span class="text-[10px] text-stone-600">{{ m.month.split(' ')[0] }}</span>
          </div>
        </div>
        <div class="text-center text-xs text-stone-600">avg rating per month</div>
      </section>

      <!-- Empty state -->
      <div v-if="stats.overview.totalItems === 0" class="text-center py-8 text-stone-500">
        <p>No data yet. Start capturing to see your stats.</p>
      </div>
    </template>
  </div>
</template>
