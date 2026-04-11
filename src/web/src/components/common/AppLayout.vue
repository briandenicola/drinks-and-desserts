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
  { name: 'Collection', path: '/items' },
  { name: 'History', path: '/history' },
  { name: 'Capture', path: '/capture' },
  { name: 'Venues', path: '/venues' },
  { name: 'Profile', path: '/profile' },
]
</script>

<template>
  <div class="min-h-screen flex flex-col">
    <!-- Header -->
    <header class="bg-stone-900 border-b border-stone-800 px-4 py-3 flex items-center justify-between safe-area-top">
      <h1 class="text-lg font-bold text-amber-500">Drinks &amp; Desserts</h1>
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

    <!-- Bottom Navigation -->
    <nav v-if="auth.isAuthenticated" class="fixed bottom-0 inset-x-0 bg-stone-900 border-t border-stone-800 safe-area-bottom">
      <div class="flex justify-around items-end pt-1 pb-2">
        <template v-for="item in navItems" :key="item.path">
          <!-- Raised Capture FAB -->
          <router-link
            v-if="item.path === '/capture'"
            :to="item.path"
            class="capture-fab flex flex-col items-center -mt-6"
          >
            <div
              class="w-14 h-14 rounded-full flex items-center justify-center shadow-lg transition-all"
              :class="route.path === '/capture'
                ? 'bg-amber-500 shadow-amber-500/30'
                : 'bg-gradient-to-br from-amber-600 to-amber-800 shadow-amber-700/20 hover:shadow-amber-500/30'"
            >
              <svg xmlns="http://www.w3.org/2000/svg" class="w-7 h-7 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
                <circle cx="12" cy="13" r="3" />
              </svg>
            </div>
            <span class="text-[10px] mt-1" :class="route.path === '/capture' ? 'text-amber-500' : 'text-stone-500'">Capture</span>
          </router-link>

          <!-- Standard nav items -->
          <router-link
            v-else
            :to="item.path"
            class="flex flex-col items-center px-3 py-1 text-xs transition-colors"
            :class="route.path === item.path ? 'text-amber-500' : 'text-stone-500'"
          >
            <!-- Collection -->
            <svg v-if="item.path === '/items'" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
            </svg>
            <!-- History -->
            <svg v-else-if="item.path === '/history'" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <!-- Stats -->
            <svg v-else-if="item.path === '/stats'" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
            <!-- Venues -->
            <svg v-else-if="item.path === '/venues'" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
              <path stroke-linecap="round" stroke-linejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            <!-- Profile -->
            <svg v-else xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 mb-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
            <span>{{ item.name }}</span>
          </router-link>
        </template>
      </div>
    </nav>
  </div>
</template>

<style scoped>
.safe-area-bottom {
  padding-bottom: max(0.5rem, env(safe-area-inset-bottom));
}
.safe-area-top {
  padding-top: max(0.75rem, env(safe-area-inset-top));
}
.capture-fab {
  position: relative;
  z-index: 1;
}
</style>
