<script setup lang="ts">
import { watch } from 'vue'
import { RouterLink } from 'vue-router'
import { getJson } from '../api/client'
import type { Product, StockLevel } from '../api/types'
import { useLoad } from '../composables/useLoad'

const props = defineProps<{ code: string }>()

interface DetailData {
  product: Product
  stock: StockLevel[]
}

// Load product and its scoped stock rows.
// Promise.allSettled keeps the calls parallel while ensuring the product's
// rejection (including 404 product_not_found) always wins over stock's 400,
// preventing a validation_failed from masking the not-found state.
// 404 product_not_found is the only named not-found case; all else is error.
const { state, load } = useLoad<DetailData>(
  async () => {
    const [productResult, stockResult] = await Promise.allSettled([
      getJson<Product>(`/products/${encodeURIComponent(props.code)}`),
      getJson<StockLevel[]>(`/stock?productCode=${encodeURIComponent(props.code)}`),
    ])
    // Product rejection always propagates first so not-found is never hidden by stock's 400.
    if (productResult.status === 'rejected') throw productResult.reason
    const stock = stockResult.status === 'fulfilled' ? stockResult.value : []
    return { product: productResult.value, stock }
  },
  { notFoundCodes: ['product_not_found'], isEmpty: () => false },
)

// Watch the code prop so the component reloads when navigating between product detail pages
// without a full remount (Vue Router reuses the instance when only the param changes).
watch(() => props.code, load, { immediate: true })
</script>

<template>
  <div>
    <p class="pd-back">
      <RouterLink to="/products">← Back to products</RouterLink>
    </p>

    <!-- Loading state -->
    <div
      v-if="state.tag === 'loading'"
      role="status"
      aria-busy="true"
      aria-live="polite"
      class="muted pd-status"
    >
      Loading product…
    </div>

    <!-- Not-found state -->
    <div v-else-if="state.tag === 'not-found'">
      <h1 class="pd-heading">Product not found</h1>
      <p class="muted">
        No product with code <strong class="pd-code-inline">{{ code }}</strong> exists.
      </p>
      <RouterLink to="/products">Back to product list</RouterLink>
    </div>

    <!-- Error state -->
    <div v-else-if="state.tag === 'error'" role="alert" class="notice">
      <p class="pd-error-msg">{{ state.message }}</p>
      <button type="button" @click="load">Retry</button>
    </div>

    <!-- Data state -->
    <template v-else-if="state.tag === 'data'">
      <h1 class="pd-heading">{{ state.value.product.code }}</h1>
      <dl class="pd-meta">
        <div class="pd-meta-row">
          <dt>Description</dt>
          <dd>{{ state.value.product.description }}</dd>
        </div>
        <div class="pd-meta-row">
          <dt class="muted">Created</dt>
          <!-- Viewer's locale timestamp -->
          <dd class="muted">{{ new Date(state.value.product.createdAt).toLocaleString() }}</dd>
        </div>
      </dl>

      <h2 class="pd-section">Stock levels</h2>

      <p v-if="state.value.stock.length === 0" class="muted">
        No stock in your warehouses.
      </p>

      <table v-else aria-label="Stock levels for this product">
        <thead>
          <tr>
            <th scope="col">Warehouse</th>
            <th scope="col" class="num">Quantity</th>
            <th scope="col">Updated</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in state.value.stock" :key="row.warehouseCode">
            <td>{{ row.warehouseCode }}</td>
            <!-- Right-aligned tabular figures via global .num class -->
            <td class="num">{{ row.quantity }}</td>
            <td>{{ new Date(row.updatedAt).toLocaleString() }}</td>
          </tr>
        </tbody>
      </table>
    </template>
  </div>
</template>

<style scoped>
.pd-back {
  margin-bottom: 1.25rem;
}
.pd-heading {
  margin: 0 0 1rem;
  font-size: 1.5rem;
  /* Prevent a long product code from causing horizontal scroll at narrow viewports */
  overflow-wrap: anywhere;
}
.pd-code-inline {
  /* Match heading: let a long code wrap rather than push content off-screen */
  overflow-wrap: anywhere;
}
.pd-meta {
  margin: 0 0 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}
.pd-meta-row {
  display: flex;
  gap: 0.75rem;
}
.pd-meta-row dt {
  font-weight: 600;
  min-width: 8rem;
}
.pd-meta-row dd {
  margin: 0;
}
.pd-section {
  margin: 1.5rem 0 0.75rem;
  font-size: 1.1rem;
}
.pd-status {
  padding: 1rem 0;
}
.pd-error-msg {
  margin: 0 0 0.75rem;
}
</style>
