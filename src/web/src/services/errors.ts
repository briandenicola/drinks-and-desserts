import type { AxiosError } from 'axios'

/**
 * Extract a user-friendly error message from an Axios error or unknown exception.
 * Falls back to the provided default message if no specific message is available.
 */
export function getErrorMessage(error: unknown, fallback: string): string {
  const axiosError = error as AxiosError<{ message?: string }>
  return axiosError?.response?.data?.message
    ?? (error instanceof Error ? error.message : null)
    ?? fallback
}
