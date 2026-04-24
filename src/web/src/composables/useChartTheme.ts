export function useChartTheme() {
  const theme = {
    color: [
      '#96BEE6', // primary blue
      '#d4956a', // amber/warm
      '#2a5299', // deep blue
      '#e87e6a', // coral
      '#4a7aa5', // mid blue
      '#c084fc', // purple
      '#34d399', // emerald
      '#f472b6', // pink
    ],
    backgroundColor: 'transparent',
    textStyle: {
      color: '#96BEE6',
    },
    title: {
      textStyle: { color: '#96BEE6', fontSize: 14 },
    },
    legend: {
      textStyle: { color: '#96BEE6' },
    },
    tooltip: {
      backgroundColor: '#041e3e',
      borderColor: '#0a2a52',
      textStyle: { color: '#96BEE6' },
    },
    categoryAxis: {
      axisLine: { lineStyle: { color: '#0a2a52' } },
      axisLabel: { color: '#4a7aa5' },
      splitLine: { lineStyle: { color: '#0a2a52' } },
    },
    valueAxis: {
      axisLine: { lineStyle: { color: '#0a2a52' } },
      axisLabel: { color: '#4a7aa5' },
      splitLine: { lineStyle: { color: '#0a2a52' } },
    },
  }

  return { theme }
}
