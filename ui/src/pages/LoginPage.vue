<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { postJson, ApiError } from '../api/client'
import { setToken } from '../api/session'

interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresAt: string
}

const router = useRouter()
const route = useRoute()

const username = ref('')
const password = ref('')
const submitting = ref(false)
const alertMessage = ref<string | null>(null)

async function submit() {
  if (submitting.value) return
  submitting.value = true
  alertMessage.value = null

  try {
    const data = await postJson<LoginResponse>('/auth/login', {
      username: username.value,
      password: password.value,
    })
    setToken(data.accessToken)

    // Redirect back to the intended page; only accept a path that starts with "/"
    // and not "//" (open-redirect) and not "/login" (would loop back to the sign-in form).
    const raw = typeof route.query.redirect === 'string' ? route.query.redirect : ''
    const redirect =
      raw.startsWith('/') && !raw.startsWith('//') && !raw.startsWith('/login')
        ? raw
        : '/products'
    await router.replace(redirect)
  } catch (err) {
    if (err instanceof ApiError) {
      alertMessage.value = err.message
    } else {
      alertMessage.value = 'An unexpected error occurred.'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <h1>Sign in</h1>

    <div
      v-if="alertMessage"
      class="notice"
      role="alert"
      aria-live="assertive"
    >
      {{ alertMessage }}
    </div>

    <form
      class="login-form"
      novalidate
      :aria-busy="submitting ? 'true' : undefined"
      @submit.prevent="submit"
    >
      <div class="field">
        <label for="login-username">Username</label>
        <input
          id="login-username"
          v-model="username"
          type="text"
          autocomplete="username"
          required
        />
      </div>

      <div class="field">
        <label for="login-password">Password</label>
        <input
          id="login-password"
          v-model="password"
          type="password"
          autocomplete="current-password"
          required
        />
      </div>

      <div class="actions">
        <button type="submit" :disabled="submitting" :aria-busy="submitting ? 'true' : undefined">
          {{ submitting ? 'Signing in…' : 'Sign in' }}
        </button>
      </div>
    </form>
  </div>
</template>

<style scoped>
.login-page {
  max-width: 24rem;
  margin: 4rem auto 0;
}

h1 {
  margin: 0 0 1.25rem;
  font-size: 1.5rem;
}

.login-form {
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
}

label {
  font-weight: 600;
  font-size: 0.875rem;
}

.actions {
  display: flex;
  justify-content: flex-end;
}

.notice {
  margin-bottom: 1rem;
}
</style>
