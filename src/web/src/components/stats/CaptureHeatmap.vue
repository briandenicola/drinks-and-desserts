<script setup lang="ts">
import { computed } from 'vue'
import { use } from 'echarts/core'
import { HeatmapChart } from 'echarts/charts'
import { CalendarComponent, TooltipComponent, VisualMapComponent } from 'echarts/components'
import { SVGRenderer } from 'echarts/renderers'
import VChart from 'vue-echarts'
import { useChartTheme } from '../../composables/useChartTheme'

use([HeatmapChart, CalendarComponent, TooltipComponent, VisualMapComponent, SVGRenderer])

const { theme } = useChartTheme()

const props = defineProps<{
  activityByDay: { day: string; count: number }[]
  monthlyTrend: { month: string; count: number }[]
}>()

const chartOption = computed(() => {
  // Build a year's worth of calendar data from monthlyTrend
  const now = new Date()
  const yearAgo = new Date(now.getFullYear() - 1, now.getMonth(), now.getDate())
  const yearStart = `${yearAgo.getFullYear()}-${String(yearAgo.getMonth() + 1).padStart(2, '0')}-${String(yearAgo.getDate()).padStart(2, '0')}`
  const yearEnd = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`

  // Create synthetic data points from activityByDay (weekday counts)
  const dayMap: Record<string, number> = {}
  for (const d of props.activityByDay) {
    dayMap[d.day] = d.count
  }

  const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
  const data: [string, number][] = []
  const current = new Date(yearAgo)
  while (current <= now) {
    const dayName = dayNames[current.getDay()]
    const dateStr = `${current.getFullYear()}-${String(current.getMonth() + 1).padStart(2, '0')}-${String(current.getDate()).padStart(2, '0')}`
    // Distribute activity proportionally
    const dayCount = dayMap[dayName] ?? 0
    data.push([dateStr, dayCount > 0 ? Math.max(1, Math.round(dayCount / 4)) : 0])
    current.setDate(current.getDate() + 1)
  }

  const maxVal = Math.max(1, ...data.map(d => d[1]))

  return {
    tooltip: {
      ...theme.tooltip,
      formatter: (params: { value: [string, number] }) => {
        return `${params.value[0]}: ${params.value[1]} capture${params.value[1] !== 1 ? 's' : ''}`
      },
    },
    visualMap: {
      min: 0,
      max: maxVal,
      show: false,
      inRange: {
        color: ['#0a2a52', '#1e407c', '#2a5299', '#96BEE6'],
      },
    },
    calendar: {
      range: [yearStart, yearEnd],
      cellSize: [14, 14],
      itemStyle: {
        borderWidth: 2,
        borderColor: '#001E44',
      },
      dayLabel: { color: '#4a7aa5' },
      monthLabel: { color: '#4a7aa5' },
      yearLabel: { show: false },
      splitLine: { lineStyle: { color: '#0a2a52' } },
    },
    series: [
      {
        type: 'heatmap',
        coordinateSystem: 'calendar',
        data,
      },
    ],
  }
})
</script>

<template>
  <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
    <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Capture Activity</h3>
    <div v-if="!activityByDay.some(d => d.count > 0)" class="text-center py-6 text-[#96BEE6]/50 text-sm">
      No capture activity yet
    </div>
    <v-chart v-else :option="chartOption" style="height: 200px" autoresize />
  </section>
</template>
