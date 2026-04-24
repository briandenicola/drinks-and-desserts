<script setup lang="ts">
import { computed } from 'vue'
import { use } from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import { SVGRenderer } from 'echarts/renderers'
import VChart from 'vue-echarts'
import { useChartTheme } from '../../composables/useChartTheme'
import type { RatingBucket } from '../../services/users'

use([BarChart, GridComponent, TooltipComponent, LegendComponent, SVGRenderer])

const { theme } = useChartTheme()

const props = defineProps<{
  buckets: RatingBucket[]
}>()

const chartOption = computed(() => {
  const ratings = [0.5, 1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, 5]

  const drinkData = ratings.map(r => {
    const bucket = props.buckets.find(b => b.rating === r && b.category === 'drink')
    return bucket?.count ?? 0
  })
  const dessertData = ratings.map(r => {
    const bucket = props.buckets.find(b => b.rating === r && b.category === 'dessert')
    return bucket?.count ?? 0
  })

  return {
    ...theme,
    tooltip: {
      ...theme.tooltip,
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
    },
    legend: {
      ...theme.legend,
      data: ['Drinks', 'Desserts'],
      bottom: 0,
    },
    grid: {
      left: '3%',
      right: '4%',
      top: '10%',
      bottom: '15%',
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: ratings.map(String),
      ...theme.categoryAxis,
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      ...theme.valueAxis,
    },
    series: [
      {
        name: 'Drinks',
        type: 'bar',
        stack: 'total',
        data: drinkData,
        itemStyle: { color: '#96BEE6' },
        barMaxWidth: 24,
      },
      {
        name: 'Desserts',
        type: 'bar',
        stack: 'total',
        data: dessertData,
        itemStyle: { color: '#d4956a' },
        barMaxWidth: 24,
      },
    ],
  }
})
</script>

<template>
  <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
    <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Ratings Distribution</h3>
    <div v-if="!buckets.length" class="text-center py-6 text-[#96BEE6]/50 text-sm">
      No rating data available yet
    </div>
    <v-chart v-else :option="chartOption" style="height: 280px" autoresize />
  </section>
</template>
