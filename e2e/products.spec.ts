// End-to-end: the Vue UI against the real composed API and PostgreSQL.
//
// How to run (from the repo root, with .env filled in from .env.example):
//   docker compose up -d                      # API on http://localhost:8080 (or API_PORT)
//   cd e2e && npm install && npx playwright install chromium
//   npx playwright test                       # starts the UI's Vite server itself
// Set API_URL when the API is not on :8080. The demo password comes from DEMO_PASSWORD,
// or else from the DemoUsers__Password line in ../.env.
import { expect, test } from '@playwright/test'
import { existsSync, readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

function demoPassword(): string {
  if (process.env.DEMO_PASSWORD) return process.env.DEMO_PASSWORD

  const envFile = resolve(dirname(fileURLToPath(import.meta.url)), '..', '.env')
  const line = existsSync(envFile)
    ? /^DemoUsers__Password=(.*)$/m.exec(readFileSync(envFile, 'utf8'))
    : null
  const value = line?.[1].trim().replace(/^(['"])(.*)\1$/, '$2')
  if (!value) {
    throw new Error(
      'No demo password: set DEMO_PASSWORD or put DemoUsers__Password=... in the repo-root .env',
    )
  }
  return value
}

test('alice logs in, sees the list, creates a product, sees the 409 on the code field when repeating it, opens the detail', async ({
  page,
}) => {
  const code = `E2E-${Date.now()}`
  const description = `Created by the e2e spec ${code}`
  const mainNav = page.getByRole('navigation', { name: 'Main' })

  // Login: an unauthenticated visit is sent to the sign-in form.
  await page.goto('/products')
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
  await page.getByLabel('Username').fill('alice')
  await page.getByLabel('Password').fill(demoPassword())
  await page.getByRole('button', { name: 'Sign in' }).click()

  // List: loaded, as a table or (on a fresh database, which seeds no products) the empty state.
  await expect(page.getByRole('heading', { name: 'Products' })).toBeVisible()
  await expect(
    page.getByRole('table', { name: 'Product list' }).or(page.getByText('No products yet.')),
  ).toBeVisible()

  // Create: success lands on the new product's detail page.
  await mainNav.getByRole('link', { name: 'New product' }).click()
  await page.getByLabel('Code').fill(code)
  await page.getByLabel('Description').fill(description)
  await page.getByRole('button', { name: 'Create product' }).click()
  await expect(page.getByRole('heading', { name: code })).toBeVisible()

  // Duplicate: the 409 is shown on the code field, and the form keeps its values.
  await mainNav.getByRole('link', { name: 'New product' }).click()
  await page.getByLabel('Code').fill(code)
  await page.getByLabel('Description').fill(description)
  await page.getByRole('button', { name: 'Create product' }).click()
  const codeField = page.getByLabel('Code')
  await expect(codeField).toHaveAttribute('aria-invalid', 'true')
  await expect(codeField).toHaveAccessibleDescription(`A product with code '${code}' already exists.`)
  await expect(codeField).toHaveValue(code)

  // Detail: open the product from the list.
  await mainNav.getByRole('link', { name: 'Products' }).click()
  await page.getByRole('table', { name: 'Product list' }).getByRole('link', { name: code }).click()
  await expect(page.getByRole('heading', { name: code })).toBeVisible()
  await expect(page.getByText(description)).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Stock levels' })).toBeVisible()
})
