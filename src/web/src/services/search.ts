import api from './api'
import type { Item } from './items'
import type { Venue } from './venues'

export interface SearchRequest {
  query: string
  mode?: 'auto' | 'instant' | 'memory'
  limit?: number
}

export interface SearchDateRange {
  start: string
  end: string
  label: string
}

export interface SearchInterpretation {
  source: 'ai-foundry' | 'rules' | string
  confidence: number
  needsSemanticSearch: boolean
  itemTypes: string[]
  dateRange?: SearchDateRange
  venueHints: string[]
  qualityHints: string[]
  terms: string[]
}

export interface SearchResult {
  kind: 'item' | 'venue'
  item?: Item
  venue?: Venue
  score: number
  reasons: string[]
}

export interface SearchResponse {
  interpretedQuery: SearchInterpretation
  results: SearchResult[]
}

export const searchApi = {
  search: (request: SearchRequest) =>
    api.post<SearchResponse>('/search', request),
}
