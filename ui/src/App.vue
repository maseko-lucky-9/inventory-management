<script setup lang="ts">
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { authenticated, clearToken } from './api/session'
import { setUnauthorizedHandler } from './api/client'

const router = useRouter()

// Wire the client's 401 handler so any expired-token response redirects to /login.
// Guard: if we're already on the login route (e.g. a second parallel 401 fires after
// the first navigation has already landed on /login), do nothing — pushing again would
// nest the redirect param (/login?redirect=/login?redirect=...).
setUnauthorizedHandler(() => {
  if (router.currentRoute.value.name === 'login') return
  const redirect = router.currentRoute.value.fullPath
  void router.push(`/login?redirect=${encodeURIComponent(redirect)}`)
})

function signOut() {
  clearToken()
  void router.push('/login')
}
</script>

<template>
  <header class="bar">
    <RouterLink to="/products" class="brand">Inventory</RouterLink>
    <nav aria-label="Main">
      <RouterLink to="/products">Products</RouterLink>
      <RouterLink to="/products/new">New product</RouterLink>
    </nav>
    <button
      v-if="authenticated"
      type="button"
      class="sign-out"
      @click="signOut"
    >
      Sign out
    </button>
  </header>
  <main class="page">
    <RouterView />
  </main>
</template>

<style scoped>
.sign-out {
  background: transparent;
  border-color: var(--border);
  color: var(--text);
  padding: 0.35rem 0.75rem;
  font-size: 0.875rem;
}
</style>
