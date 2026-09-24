import { ref } from 'vue'

// Shared draft that outlives the create-page component, surviving a 401 -> login -> back round
// trip in memory (US5 / spec edge case). Cleared after a successful create.
export const draftCode = ref('')
export const draftDescription = ref('')

export function clearDraft(): void {
  draftCode.value = ''
  draftDescription.value = ''
}
