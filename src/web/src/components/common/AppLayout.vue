<script setup lang="ts">
import { provide } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { useRoute } from 'vue-router'
import { usePullToRefresh } from '../../composables/usePullToRefresh'
import { RefreshKey } from '../../composables/refreshKey'

const auth = useAuthStore()
const route = useRoute()

const {
  isRefreshing,
  pullDistance,
  pullProgress,
  setRefreshCallback,
  onTouchStart,
  onTouchMove,
  onTouchEnd,
} = usePullToRefresh()

// Views call this to register their refresh function
provide(RefreshKey, (fn: () => Promise<void>) => setRefreshCallback(fn))

const navItems = [
  { name: 'Capture', path: '/' },
  { name: 'Collection', path: '/items' },
  { name: 'History', path: '/history' },
  { name: 'Profile', path: '/profile' },
]
</script>

<template>
  <div class="min-h-screen flex flex-col">
    <!-- Header -->
    <header class="bg-stone-900 border-b border-stone-800 px-4 py-3 flex items-center justify-between">
      <h1 class="text-lg font-bold text-amber-500">Whiskey &amp; Smokes</h1>
      <button
        v-if="auth.isAuthenticated"
        @click="auth.logout()"
        class="text-sm text-stone-400 hover:text-stone-200"
      >
        Sign Out
      </button>
    </header>

    <!-- Pull-to-refresh indicator -->
    <div
      class="flex justify-center overflow-hidden transition-all duration-200"
      :style="{ height: pullDistance + 'px' }"
    >
      <div class="flex items-center justify-center h-full">
        <svg
          v-if="isRefreshing"
          class="w-5 h-5 text-amber-500 animate-spin"
          xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"
        >
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
        <svg
          v-else
          class="w-5 h-5 text-stone-500 transition-transform duration-200"
          :style="{ transform: `rotate(${pullProgress * 180}deg)`, opacity: pullProgress }"
          xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"
        >
          <path stroke-linecap="round" stroke-linejoin="round" d="M19 14l-7 7m0 0l-7-7m7 7V3" />
        </svg>
      </div>
    </div>

    <!-- Main Content -->
    <main
      class="flex-1 overflow-y-auto pb-20"
      @touchstart.passive="onTouchStart"
      @touchmove.passive="onTouchMove"
      @touchend="onTouchEnd"
    >
      <slot />
    </main>

    <!-- Bottom Navigation (mobile) -->
    <nav v-if="auth.isAuthenticated" class="fixed bottom-0 inset-x-0 bg-stone-900 border-t border-stone-800 flex justify-around py-2 safe-area-bottom">
      <router-link
        v-for="item in navItems"
        :key="item.path"
        :to="item.path"
        class="flex flex-col items-center px-3 py-1 text-xs transition-colors"
        :class="route.path === item.path ? 'text-amber-500' : 'text-stone-500'"
      >
        <!-- Capture -->
        <svg v-if="item.path === '/'" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
          <circle cx="12" cy="13" r="3" />
        </svg>
        <!-- Collection -->
        <svg v-else-if="item.path === '/items'" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
        </svg>
        <!-- History -->
        <svg v-else-if="item.path === '/history'" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <!-- Profile -->
        <svg v-else xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
        </svg>
        <span>{{ item.name }}</span>
      </router-link>
    </nav>
  </div>
</template>

<style scoped>
.safe-area-bottom {
  padding-bottom: max(0.5rem, env(safe-area-inset-bottom));
}
</style>
