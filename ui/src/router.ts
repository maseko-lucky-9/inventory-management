import { createRouter, createWebHistory } from 'vue-router'
import ProductDetailPage from './pages/ProductDetailPage.vue'
import ProductListPage from './pages/ProductListPage.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/products' },
    { path: '/products', name: 'products', component: ProductListPage },
    { path: '/products/:code', name: 'product', component: ProductDetailPage, props: true },
  ],
})
