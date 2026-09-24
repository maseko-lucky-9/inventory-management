import { ref } from 'vue'
import type { Ref } from 'vue'
import { ApiError } from '../api/client'

// Explicit state union shared by every data-fetching page (ADR-007).
export type LoadState<T> =
  | { tag: 'loading' }
  | { tag: 'empty' }
  | { tag: 'data'; value: T }
  | { tag: 'error'; message: string }
  | { tag: 'not-found' }

interface UseLoadOptions<T> {
  /** Return true when the fetched value counts as "empty" (default: array length 0). */
  isEmpty?: (v: T) => boolean
  /** ApiError codes that map to the not-found state instead of error. */
  notFoundCodes?: readonly string[]
}

export function useLoad<T>(
  fetcher: () => Promise<T>,
  options?: UseLoadOptions<T>,
): { state: Ref<LoadState<T>>; load: () => Promise<void> } {
  const state: Ref<LoadState<T>> = ref({ tag: 'loading' })
  // Sequencing guard: only the most-recently-started load's response is applied.
  let callId = 0

  async function load(): Promise<void> {
    const myId = ++callId
    state.value = { tag: 'loading' }
    try {
      const result = await fetcher()
      if (myId !== callId) return // stale response superseded by a later load
      const checkEmpty = options?.isEmpty
        ? options.isEmpty(result)
        : Array.isArray(result) && (result as unknown[]).length === 0
      state.value = checkEmpty ? { tag: 'empty' } : { tag: 'data', value: result }
    } catch (e) {
      if (myId !== callId) return // stale rejection superseded by a later load
      // 401: client.ts already cleared the token and queued a /login redirect; stay loading
      // so the page never flashes an error before the navigation completes.
      if (e instanceof ApiError && e.status === 401) return
      if (e instanceof ApiError && options?.notFoundCodes?.includes(e.code)) {
        state.value = { tag: 'not-found' }
      } else {
        state.value = {
          tag: 'error',
          message: e instanceof ApiError ? e.message : 'Something went wrong.',
        }
      }
    }
  }

  return { state, load }
}
