<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { getJson } from '../api/client'
import type { Product } from '../api/types'
import { useLoad } from '../composables/useLoad'

const { state, load } = useLoad<Product[]>(() => getJson<Product[]>('/products'))

onMounted(load)
</script>

<template>
  <div>
    <div class="pl-bar">
      <h1 class="pl-heading">Products</h1>
    </div>

    <!-- Loading state -->
    <div
      v-if="state.tag === 'loading'"
      role="status"
      aria-busy="true"
      aria-live="polite"
      class="pl-notice muted"
    >
      Loading products…
    </div>

    <!-- Empty state -->
    <p v-else-if="state.tag === 'empty'" class="pl-notice muted">
      No products yet.
    </p>

    <!-- Error state -->
    <div v-else-if="state.tag === 'error'" role="alert" class="notice">
      <p class="pl-error-msg">{{ state.message }}</p>
      <button type="button" @click="load">Retry</button>
    </div>

    <!-- Data state: outer div allows horizontal scroll on narrow viewports without breaking layout -->
    <div v-else-if="state.tag === 'data'" class="pl-scroll">
      <table aria-label="Product list">
        <thead>
          <tr>
            <th scope="col">Code</th>
            <th scope="col">Description</th>
            <th scope="col">Created</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="p in state.value" :key="p.code">
            <!-- nowrap keeps a code like SKU-1 on one line and lets the scroll container handle overflow -->
            <td class="pl-code">
              <RouterLink :to="`/products/${encodeURIComponent(p.code)}`">{{ p.code }}</RouterLink>
            </td>
            <td>{{ p.description }}</td>
            <!-- Viewer's locale timestamp -->
            <td>{{ new Date(p.createdAt).toLocaleString() }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.pl-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1.25rem;
  flex-wrap: wrap;
}
.pl-heading {
  margin: 0;
  font-size: 1.5rem;
}
.pl-notice {
  padding: 1rem 0;
}
.pl-error-msg {
  margin: 0 0 0.75rem;
}
/* Horizontal scroll wrapper: table expands naturally; viewport clips cleanly */
.pl-scroll {
  overflow-x: auto;
}
/* Keep a code like SKU-1 on one line; the scroll wrapper handles wider codes */
.pl-code {
  white-space: nowrap;
}
</style>
