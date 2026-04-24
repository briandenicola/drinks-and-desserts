import { defineStore } from 'pinia'
import { ref } from 'vue'
import { usersApi, type DashboardStats, type RatingBucket } from '../services/users'

export const useDashboardStore = defineStore('dashboard', () => {
  const summary = ref<DashboardStats['summary'] | null>(null)
  const thisMonth = ref<DashboardStats['thisMonth'] | null>(null)
  const recentActivity = ref<DashboardStats['recentActivity']>([])
  const ratingDistribution = ref<RatingBucket[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  async function loadDashboard() {
    isLoading.value = true
    error.value = null
    try {
      const { data } = await usersApi.getDashboard()
      summary.value = data.summary
      thisMonth.value = data.thisMonth
      recentActivity.value = data.recentActivity
    } catch {
      error.value = 'Failed to load dashboard data'
    } finally {
      isLoading.value = false
    }
  }

  async function loadRatingDistribution() {
    try {
      const { data } = await usersApi.getRatingDistribution()
      ratingDistribution.value = data.buckets
    } catch {
      // Non-critical — chart will show empty state
    }
  }

  async function loadAll() {
    await Promise.all([loadDashboard(), loadRatingDistribution()])
  }

  return {
    summary, thisMonth, recentActivity, ratingDistribution,
    isLoading, error,
    loadDashboard, loadRatingDistribution, loadAll,
  }
})
