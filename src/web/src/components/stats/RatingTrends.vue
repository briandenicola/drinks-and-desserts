<script setup lang="ts">
import { computed } from 'vue'
import { use } from 'echarts/core'
import { LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import { SVGRenderer } from 'echarts/renderers'
import VChart from 'vue-echarts'
import { useChartTheme } from '../../composables/useChartTheme'

use([LineChart, GridComponent, TooltipComponent, SVGRenderer])

const { theme } = useChartTheme()

const props = defineProps<{
  ratingTrend: { month: string; avgRating: number; count: number }[]
}>()

const chartOption = computed(() => {
  const months = props.ratingTrend.map(m => m.month)
  const ratings = props.ratingTrend.map(m => m.avgRating || null)

  return {
    tooltip: {
      ...theme.tooltip,
      trigger: 'axis',
    },
    grid: {
      left: '3%', right: '4%', top: '10%', bottom: '10%',
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: months,
      ...theme.categoryAxis,
    },
    yAxis: {
      type: 'value',
      min: 0,
      max: 5,
      ...theme.valueAxis,
    },
    series: [
      {
        type: 'line',
        data: ratings,
        smooth: true,
        lineStyle: { color: '#96BEE6', width: 3 },
        itemStyle: { color: '#96BEE6' },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: 'rgba(150,190,230,0.3)' },
              { offset: 1, color: 'rgba(150,190,230,0.05)' },
            ],
          },
        },
        connectNulls: true,
      },
    ],
  }
})
</script>

<template>
  <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
    <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Rating Trends</h3>
    <div v-if="!ratingTrend.some(m => m.count > 0)" class="text-center py-6 text-[#96BEE6]/50 text-sm">
      Not enough rating data yet
    </div>
    <v-chart v-else :option="chartOption" style="height: 280px" autoresize />
  </section>
</template>
