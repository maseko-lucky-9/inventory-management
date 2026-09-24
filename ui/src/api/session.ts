import { ref } from 'vue'
import type { Ref } from 'vue'

// In-memory token store backed by Vue refs so components react when the token changes.
// Never persisted to localStorage, sessionStorage or cookies.
const _token = ref<string | null>(null)

/** Reactive boolean: true while a token is held in memory. */
export const authenticated: Ref<boolean> = ref(false)

export function getToken(): string | null {
  return _token.value
}

export function setToken(token: string): void {
  _token.value = token
  authenticated.value = true
}

export function clearToken(): void {
  _token.value = null
  authenticated.value = false
}

export function isAuthenticated(): boolean {
  return _token.value !== null
}
