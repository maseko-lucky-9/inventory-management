<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <header class="bar">
    <RouterLink to="/products" class="brand">Inventory</RouterLink>
    <nav aria-label="Main">
      <RouterLink to="/products">Products</RouterLink>
      <RouterLink to="/products/new">New product</RouterLink>
    </nav>
  </header>
  <main class="page">
    <RouterView />
  </main>
</template>
