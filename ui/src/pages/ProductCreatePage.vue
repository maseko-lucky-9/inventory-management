<script setup lang="ts">
import { ref, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { postJson, ApiError } from '../api/client'
import { draftCode, draftDescription, clearDraft } from '../api/draftProduct'
import type { Product } from '../api/types'

const router = useRouter()

// Draft lives in a shared module so it survives a 401 -> login -> back round trip (US5).
const code = draftCode
const description = draftDescription

// Submission state
const submitting = ref(false)

// Per-field server errors (keyed by camelCase JSON name matching the API's errors map)
const fieldErrors = ref<Record<string, string[]>>({})

// Form-level alert for non-field errors (network, 5xx, etc.)
const formAlert = ref<string | null>(null)

// DOM refs for focus management
const codeInput = ref<HTMLInputElement | null>(null)
const descriptionInput = ref<HTMLTextAreaElement | null>(null)

function clearFieldError(field: 'code' | 'description') {
  // Clear server error when the user edits that field
  if (fieldErrors.value[field]?.length) {
    const updated = { ...fieldErrors.value }
    delete updated[field]
    fieldErrors.value = updated
  }
}

async function submit() {
  if (submitting.value) return
  submitting.value = true
  fieldErrors.value = {}
  formAlert.value = null

  try {
    const product = await postJson<Product>('/products', {
      code: code.value,
      description: description.value,
    })
    // Clear the shared draft only after a confirmed successful create.
    clearDraft()
    // Navigate to the new product's detail page on success
    await router.push(`/products/${encodeURIComponent(product.code)}`)
  } catch (err) {
    if (err instanceof ApiError) {
      if (err.status === 401) {
        // client.ts cleared the token and queued a /login redirect; draft is preserved in
        // the shared module — do nothing else here.
        return
      } else if (err.status === 400 && err.code === 'validation_failed') {
        // Map field-level errors from the API's errors object
        const knownFields: ReadonlyArray<string> = ['code', 'description']
        const fieldMap: Record<string, string[]> = {}
        const unknown: string[] = []

        for (const [key, messages] of Object.entries(err.errors)) {
          if (knownFields.includes(key)) {
            fieldMap[key] = [...messages]
          } else {
            unknown.push(...messages)
          }
        }

        fieldErrors.value = fieldMap

        // Any unknown-key messages go to the form-level alert
        if (unknown.length) {
          formAlert.value = unknown.join(' ')
        }

        // Move focus to the first invalid field
        await nextTick()
        if (fieldMap['code']) {
          codeInput.value?.focus()
        } else if (fieldMap['description']) {
          descriptionInput.value?.focus()
        }
      } else if (err.status === 409 && err.code === 'duplicate_product_code') {
        // Show the server message under the code field; form stays filled
        fieldErrors.value = { code: [err.message] }
        await nextTick()
        codeInput.value?.focus()
      } else {
        // Network / 5xx — form-level alert; form stays filled
        formAlert.value = err.message
      }
    } else {
      formAlert.value = 'An unexpected error occurred.'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="create-page">
    <h1>New product</h1>

    <!-- Form-level alert for network / 5xx errors -->
    <div
      v-if="formAlert"
      class="notice"
      role="alert"
      aria-live="assertive"
    >
      {{ formAlert }}
    </div>

    <form
      class="create-form"
      novalidate
      :aria-busy="submitting ? 'true' : undefined"
      @submit.prevent="submit"
    >
      <!-- Code field -->
      <div class="field">
        <label for="product-code">Code</label>
        <input
          id="product-code"
          ref="codeInput"
          v-model="code"
          type="text"
          autocomplete="off"
          :aria-invalid="fieldErrors.code?.length ? 'true' : undefined"
          :aria-describedby="fieldErrors.code?.length ? 'code-error' : undefined"
          @input="clearFieldError('code')"
        />
        <ul
          v-if="fieldErrors.code?.length"
          id="code-error"
          class="field-errors"
          role="alert"
        >
          <li v-for="msg in fieldErrors.code" :key="msg">{{ msg }}</li>
        </ul>
      </div>

      <!-- Description field -->
      <div class="field">
        <label for="product-description">Description</label>
        <textarea
          id="product-description"
          ref="descriptionInput"
          v-model="description"
          rows="3"
          :aria-invalid="fieldErrors.description?.length ? 'true' : undefined"
          :aria-describedby="fieldErrors.description?.length ? 'description-error' : undefined"
          @input="clearFieldError('description')"
        ></textarea>
        <ul
          v-if="fieldErrors.description?.length"
          id="description-error"
          class="field-errors"
          role="alert"
        >
          <li v-for="msg in fieldErrors.description" :key="msg">{{ msg }}</li>
        </ul>
      </div>

      <div class="actions">
        <button type="submit" :disabled="submitting" :aria-busy="submitting ? 'true' : undefined">
          {{ submitting ? 'Creating…' : 'Create product' }}
        </button>
      </div>
    </form>
  </div>
</template>

<style scoped>
.create-page {
  max-width: 36rem;
}

h1 {
  margin: 0 0 1.25rem;
  font-size: 1.5rem;
}

.create-form {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  /* Prevent long unbreakable codes from pushing the card wider */
  min-width: 0;
}

label {
  font-weight: 600;
  font-size: 0.875rem;
}

/* Textarea shares the same token-based styles as input (style.css covers input/textarea) */
textarea {
  resize: vertical;
}

.field-errors {
  /* Inline validation messages below the field */
  list-style: none;
  margin: 0;
  padding: 0;
  color: var(--danger);
  font-size: 0.875rem;
  /* Wrap long unbreakable error text (e.g. 50-char product codes) */
  overflow-wrap: anywhere;
}

.field-errors li + li {
  margin-top: 0.2rem;
}

.notice {
  margin-bottom: 1rem;
  /* Wrap long unbreakable text (e.g. error messages with long codes) */
  overflow-wrap: anywhere;
}

.actions {
  display: flex;
  justify-content: flex-end;
}
</style>
