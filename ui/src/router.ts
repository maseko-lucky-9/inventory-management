import { createRouter, createWebHistory } from 'vue-router'
import { isAuthenticated } from './api/session'
import ProductCreatePage from './pages/ProductCreatePage.vue'
import ProductDetailPage from './pages/ProductDetailPage.vue'
import ProductListPage from './pages/ProductListPage.vue'
import LoginPage from './pages/LoginPage.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: LoginPage, meta: { public: true } },
    { path: '/', redirect: '/products' },
    { path: '/products', name: 'products', component: ProductListPage },
    { path: '/products/new', name: 'product-create', component: ProductCreatePage },
    { path: '/products/:code', name: 'product', component: ProductDetailPage, props: true },
  ],
})

// Redirect unauthenticated users to /login, carrying the intended path as ?redirect=<path>.
// Also redirect an already-authenticated user away from /login (prevents the sign-in form
// from rendering with "Sign out" visible after a parallel-401-triggered navigation).
router.beforeEach((to) => {
  if (to.name === 'login' && isAuthenticated()) return { name: 'products' }
  if (to.meta.public) return true
  if (isAuthenticated()) return true
  // Carry the intended destination so LoginPage can redirect back after login.
  return { name: 'login', query: { redirect: to.fullPath } }
})
