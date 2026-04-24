<script setup lang="ts">
import { inject, onMounted } from 'vue'
import { useDashboardStore } from '../stores/dashboard'
import { RefreshKey } from '../composables/refreshKey'
import SummaryCards from '../components/dashboard/SummaryCards.vue'
import ActivityTimeline from '../components/dashboard/ActivityTimeline.vue'
import MonthlySnapshot from '../components/dashboard/MonthlySnapshot.vue'
import RatingsChart from '../components/dashboard/RatingsChart.vue'

const dashboard = useDashboardStore()
const registerRefresh = inject(RefreshKey)

registerRefresh?.(dashboard.loadAll)
onMounted(dashboard.loadAll)
</script>

<template>
  <div class="p-4 lg:p-6 max-w-6xl mx-auto space-y-6">
    <h2 class="text-xl font-semibold text-white">Dashboard</h2>

    <!-- Loading -->
    <div v-if="dashboard.isLoading" class="text-center py-12 text-[#96BEE6]/70">Loading dashboard...</div>

    <!-- Error -->
    <div v-else-if="dashboard.error" class="text-center py-12 text-red-400">{{ dashboard.error }}</div>

    <!-- Content -->
    <template v-else-if="dashboard.summary">
      <!-- Empty state -->
      <div v-if="dashboard.summary.totalItems === 0 && dashboard.summary.wishlistSize === 0" class="text-center py-12 text-[#96BEE6]/70">
        <p class="text-lg mb-2">Welcome to Drinks &amp; Desserts</p>
        <p class="text-sm">Use the mobile app to capture your first item and start building your collection.</p>
      </div>

      <template v-else>
        <SummaryCards :summary="dashboard.summary" />

        <div class="grid lg:grid-cols-2 gap-6">
          <MonthlySnapshot v-if="dashboard.thisMonth" :snapshot="dashboard.thisMonth" />
          <RatingsChart :buckets="dashboard.ratingDistribution" />
        </div>

        <ActivityTimeline :activities="dashboard.recentActivity" />
      </template>
    </template>
  </div>
</template>
