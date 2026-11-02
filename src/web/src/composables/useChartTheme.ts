import { reactive, watch } from 'vue'
import { useAuthStore } from '../stores/auth'

export interface ChartColors {
  accent: string
  brand: string
  brandHover: string
  muted: string
  line: string
  inset: string
  page: string
}

// Original palette, used when no stylesheet is available
const FALLBACK: ChartColors = {
  accent: '#96bee6',
  brand: '#1e407c',
  brandHover: '#2a5299',
  muted: '#4a7aa5',
  line: '#0a2a52',
  inset: '#041e3e',
  page: '#001e44',
}

const VARIABLES: Record<keyof ChartColors, string> = {
  accent: '--th-accent',
  brand: '--th-brand',
  brandHover: '--th-brand-hover',
  muted: '--th-muted',
  line: '--th-line',
  inset: '--th-inset',
  page: '--th-page',
}

// ECharts draws to canvas/SVG, so it needs resolved colours rather than CSS classes
export function readChartColors(): ChartColors {
  if (typeof document === 'undefined') return { ...FALLBACK }
  const style = getComputedStyle(document.documentElement)
  const colors = { ...FALLBACK }
  for (const key of Object.keys(VARIABLES) as (keyof ChartColors)[]) {
    const value = style.getPropertyValue(VARIABLES[key]).trim()
    if (value) colors[key] = value
  }
  return colors
}

export function withAlpha(color: string, alpha: number): string {
  const hex = /^#([0-9a-f]{6})$/i.exec(color)
  if (hex) {
    const n = parseInt(hex[1], 16)
    return `rgba(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}, ${alpha})`
  }
  const rgb = /^rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/i.exec(color)
  if (rgb) return `rgba(${rgb[1]}, ${rgb[2]}, ${rgb[3]}, ${alpha})`
  return color
}

export function buildChartTheme(c: ChartColors) {
  return {
    // Accent/blue slots follow the palette; the warm and categorical hues keep
    // their meaning across themes
    color: [
      c.accent,
      '#d4956a', // amber/warm
      c.brandHover,
      '#e87e6a', // coral
      c.muted,
      '#c084fc', // purple
      '#34d399', // emerald
      '#f472b6', // pink
    ],
    backgroundColor: 'transparent',
    textStyle: {
      color: c.accent,
    },
    title: {
      textStyle: { color: c.accent, fontSize: 14 },
    },
    legend: {
      textStyle: { color: c.accent },
    },
    tooltip: {
      backgroundColor: c.inset,
      borderColor: c.line,
      textStyle: { color: c.accent },
    },
    categoryAxis: {
      axisLine: { lineStyle: { color: c.line } },
      axisLabel: { color: c.muted },
      splitLine: { lineStyle: { color: c.line } },
    },
    valueAxis: {
      axisLine: { lineStyle: { color: c.line } },
      axisLabel: { color: c.muted },
      splitLine: { lineStyle: { color: c.line } },
    },
  }
}

// `theme` and `colors` are reactive: chart options computed from them rebuild
// when the signed-in account's theme changes.
export function useChartTheme() {
  const auth = useAuthStore()
  const colors = reactive<ChartColors>(readChartColors())
  const theme = reactive(buildChartTheme(colors))

  watch(
    () => auth.user?.preferences?.theme,
    () => {
      Object.assign(colors, readChartColors())
      Object.assign(theme, buildChartTheme(colors))
    },
    { flush: 'post' },
  )

  return { theme, colors }
}
