<script setup lang="ts">
import { computed } from 'vue'
import { use } from 'echarts/core'
import { PieChart } from 'echarts/charts'
import { TooltipComponent, LegendComponent } from 'echarts/components'
import { SVGRenderer } from 'echarts/renderers'
import VChart from 'vue-echarts'
import { useChartTheme } from '../../composables/useChartTheme'

use([PieChart, TooltipComponent, LegendComponent, SVGRenderer])

const { theme } = useChartTheme()

const props = defineProps<{
  typeBreakdown: { type: string; count: number }[]
}>()

const drinkTypes = new Set(['whiskey', 'wine', 'cocktail', 'vodka', 'gin'])

const chartOption = computed(() => {
  const drinks = props.typeBreakdown.filter(t => drinkTypes.has(t.type))
  const desserts = props.typeBreakdown.filter(t => t.type === 'dessert')
  const other = props.typeBreakdown.filter(t => !drinkTypes.has(t.type) && t.type !== 'dessert')

  const data = [
    ...drinks.map(t => ({ name: capitalize(t.type), value: t.count })),
    ...desserts.map(t => ({ name: 'Dessert', value: t.count })),
    ...other.map(t => ({ name: capitalize(t.type), value: t.count })),
  ]

  return {
    tooltip: {
      ...theme.tooltip,
      trigger: 'item',
      formatter: '{b}: {c} ({d}%)',
    },
    legend: {
      ...theme.legend,
      orient: 'horizontal' as const,
      bottom: 0,
    },
    series: [
      {
        type: 'pie',
        radius: ['40%', '70%'],
        avoidLabelOverlap: true,
        itemStyle: { borderRadius: 4, borderColor: '#041e3e', borderWidth: 2 },
        label: { show: false },
        emphasis: {
          label: { show: true, fontSize: 14, color: '#96BEE6' },
        },
        data,
      },
    ],
  }
})

function capitalize(s: string) {
  return s.charAt(0).toUpperCase() + s.slice(1)
}
</script>

<template>
  <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
    <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Category Breakdown</h3>
    <div v-if="!typeBreakdown.length" class="text-center py-6 text-[#96BEE6]/50 text-sm">
      No data available yet
    </div>
    <v-chart v-else :option="chartOption" style="height: 300px" autoresize />
  </section>
</template>
