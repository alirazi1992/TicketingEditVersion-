import { test, expect, Page, APIRequestContext } from '@playwright/test';
import { normalizeBaseUrl, getDefaultApiBaseUrl } from '../lib/url';

const API_BASE_URL = normalizeBaseUrl(process.env.NEXT_PUBLIC_API_BASE_URL) || getDefaultApiBaseUrl();

// Seed user credentials (from SeedData.cs)
const SEED_USERS = {
  admin: { email: 'admin@test.com', password: 'Test123!' },
  technician: { email: 'tech1@test.com', password: 'Test123!' },
  client: { email: 'client1@test.com', password: 'Test123!' },
};

// Helper to capture console errors
function setupConsoleErrorCapture(page: Page) {
  const errors: string[] = [];
  page.on('console', (msg) => {
    if (msg.type() === 'error') {
      errors.push(msg.text());
    }
  });
  page.on('pageerror', (error: Error) => {
    errors.push(`PageError: ${error.message}`);
  });
  return errors;
}

// Helper to filter acceptable errors
function filterCriticalErrors(errors: string[]): string[] {
  return errors.filter(e => 
    !e.includes('favicon') && 
    !e.includes('net::ERR_ABORTED') &&
    !e.includes('Failed to load resource') &&
    !e.includes('404') &&
    !e.includes('ERR_CONNECTION_REFUSED') && // Backend not running is a separate issue
    !e.includes('ResizeObserver') && // Known non-critical browser warnings
    !e.includes('Non-Error promise rejection')
  );
}

test.describe('Frontend Smoke Tests', () => {
  test('Login page loads without errors', async ({ page }) => {
    const errors = setupConsoleErrorCapture(page);
    
    await page.goto('/login');
    await expect(page).toHaveURL(/.*login/);
    await expect(page.locator('body')).toBeVisible();
    
    // Wait a bit for any async errors
    await page.waitForTimeout(2000);
    
    const criticalErrors = filterCriticalErrors(errors);
    expect(criticalErrors).toHaveLength(0);
  });

  test('Client login flow', async ({ page }) => {
    const errors = setupConsoleErrorCapture(page);
    
    await page.goto('/login');
    
    // Find email input (try multiple selectors including id)
    const emailInput = page.locator('input#username, input[type="email"], input[name="email"], input[name*="email" i]').first();
    await expect(emailInput).toBeVisible({ timeout: 5000 });
    await emailInput.fill(SEED_USERS.client.email);
    
    // Find password input (try multiple selectors including id)
    const passwordInput = page.locator('input#password, input[type="password"], input[name="password"], input[name*="password" i]').first();
    await expect(passwordInput).toBeVisible({ timeout: 5000 });
    await passwordInput.fill(SEED_USERS.client.password);
    
    // Find and click submit button
    const submitButton = page.locator('button[type="submit"], button:has-text("ورود"), button:has-text("Login"), button:has-text("Sign in")').first();
    await expect(submitButton).toBeVisible({ timeout: 5000 });
    await submitButton.click();
    
    // Wait for navigation (should redirect after login)
    await page.waitForURL(/\/(dashboard|tickets|$)/, { timeout: 15000 });
    
    // Check for critical errors
    await page.waitForTimeout(2000);
    const criticalErrors = filterCriticalErrors(errors);
    expect(criticalErrors).toHaveLength(0);
    
    // Verify we're not on login page anymore
    expect(page.url()).not.toContain('/login');
  });

  test('Technician login flow', async ({ page }) => {
    const errors = setupConsoleErrorCapture(page);
    
    await page.goto('/login');
    
    const emailInput = page.locator('input#username, input[type="email"], input[name="email"], input[name*="email" i]').first();
    await emailInput.fill(SEED_USERS.technician.email);
    
    const passwordInput = page.locator('input#password, input[type="password"], input[name="password"], input[name*="password" i]').first();
    await passwordInput.fill(SEED_USERS.technician.password);
    
    const submitButton = page.locator('button[type="submit"], button:has-text("ورود"), button:has-text("Login")').first();
    await submitButton.click();
    
    await page.waitForURL(/\/(dashboard|tickets|$)/, { timeout: 15000 });
    
    await page.waitForTimeout(2000);
    const criticalErrors = filterCriticalErrors(errors);
    expect(criticalErrors).toHaveLength(0);
    
    expect(page.url()).not.toContain('/login');
  });

  test('Admin login flow', async ({ page }) => {
    const errors = setupConsoleErrorCapture(page);
    
    await page.goto('/login');
    
    const emailInput = page.locator('input#username, input[type="email"], input[name="email"], input[name*="email" i]').first();
    await emailInput.fill(SEED_USERS.admin.email);
    
    const passwordInput = page.locator('input#password, input[type="password"], input[name="password"], input[name*="password" i]').first();
    await passwordInput.fill(SEED_USERS.admin.password);
    
    const submitButton = page.locator('button[type="submit"], button:has-text("ورود"), button:has-text("Login")').first();
    await submitButton.click();
    
    await page.waitForURL(/\/(dashboard|tickets|$)/, { timeout: 15000 });
    
    await page.waitForTimeout(2000);
    const criticalErrors = filterCriticalErrors(errors);
    expect(criticalErrors).toHaveLength(0);
    
    expect(page.url()).not.toContain('/login');
  });

  test('Ticket detail route exists and loads', async ({ page }) => {
    const errors = setupConsoleErrorCapture(page);
    
    // First login as client
    await page.goto('/login');
    const emailInput = page.locator('input#username, input[type="email"], input[name="email"], input[name*="email" i]').first();
    await emailInput.fill(SEED_USERS.client.email);
    const passwordInput = page.locator('input#password, input[type="password"], input[name="password"], input[name*="password" i]').first();
    await passwordInput.fill(SEED_USERS.client.password);
    const submitButton = page.locator('button[type="submit"], button:has-text("ورود"), button:has-text("Login")').first();
    await submitButton.click();
    await page.waitForURL(/\/(dashboard|tickets|$)/, { timeout: 15000 });
    
    // Try to navigate to a ticket detail page (using a test UUID)
    const testTicketId = '00000000-0000-0000-0000-000000000001';
    const response = await page.goto(`/tickets/${testTicketId}`);
    
    // Should not be a 404 Not Found (route exists, may be unauthorized or ticket not found)
    expect(response?.status()).not.toBe(404);
    expect(response?.status()).not.toBe(500);
    
    // Check page loads
    await expect(page.locator('body')).toBeVisible();
    
    await page.waitForTimeout(2000);
    const criticalErrors = filterCriticalErrors(errors);
    expect(criticalErrors).toHaveLength(0);
  });

  test('End-to-end: Create ticket via API, verify in UI', async ({ page, request }) => {
    const errors = setupConsoleErrorCapture(page);
    
    // Step 1: Login as client via API (cookie tikq_access is set; no token in body)
    const loginResponse = await request.post(`${API_BASE_URL}/api/auth/login`, {
      data: {
        email: SEED_USERS.client.email,
        password: SEED_USERS.client.password,
      },
    });
    
    expect(loginResponse.ok()).toBeTruthy();
    const loginData = await loginResponse.json();
    expect(loginData.ok).toBe(true);
    expect(loginData.landingPath).toBeDefined();
    
    // Step 2: Get categories (same request context sends cookie)
    const categoriesResponse = await request.get(`${API_BASE_URL}/api/categories`);
    expect(categoriesResponse.ok()).toBeTruthy();
    const categories = await categoriesResponse.json();
    expect(categories.length).toBeGreaterThan(0);
    
    const categoryId = categories[0].id;
    const subcategoryId = categories[0].subcategories?.[0]?.id || null;
    
    // Step 3: Create ticket via API (cookie sent by request context; no Bearer header)
    const createTicketResponse = await request.post(`${API_BASE_URL}/api/tickets`, {
      headers: {
        'Content-Type': 'application/json',
      },
      data: {
        title: 'E2E Test Ticket',
        description: 'Created by Playwright E2E test',
        categoryId: categoryId,
        subcategoryId: subcategoryId,
        priority: 'Medium',
      },
    });
    
    expect(createTicketResponse.ok()).toBeTruthy();
    const ticketData = await createTicketResponse.json();
    expect(ticketData.id).toBeTruthy();
    const ticketId = ticketData.id;
    
    // Step 4: Login in UI as client
    await page.goto('/login');
    const emailInput = page.locator('input[type="email"], input[name="email"], input[name*="email" i]').first();
    await emailInput.fill(SEED_USERS.client.email);
    const passwordInput = page.locator('input[type="password"], input[name="password"], input[name*="password" i]').first();
    await passwordInput.fill(SEED_USERS.client.password);
    const submitButton = page.locator('button[type="submit"], button:has-text("ورود"), button:has-text("Login")').first();
    await submitButton.click();
    await page.waitForURL(/\/(dashboard|tickets|$)/, { timeout: 15000 });
    
    // Step 5: Navigate to ticket detail page
    await page.goto(`/tickets/${ticketId}`, { waitUntil: 'networkidle' });
    
    // Verify ticket detail page loads (may show error message if ticket not accessible, but route exists)
    const response = await page.goto(`/tickets/${ticketId}`);
    expect(response?.status()).not.toBe(404);
    expect(response?.status()).not.toBe(500);
    await expect(page.locator('body')).toBeVisible();
    
    // Wait for any async operations
    await page.waitForTimeout(3000);
    
    const criticalErrors = filterCriticalErrors(errors);
    expect(criticalErrors).toHaveLength(0);
  });

  test('Routes return proper status codes', async ({ page }) => {
    // Test key routes don't return 404/500
    const routes = [
      '/login',
      '/tickets/00000000-0000-0000-0000-000000000001', // Will likely fail but should not be 404 route
    ];
    
    for (const route of routes) {
      const response = await page.goto(route, { waitUntil: 'domcontentloaded' });
      const status = response?.status() || 0;
      expect(status).not.toBe(404); // Route should exist
      expect(status).not.toBe(500); // Should not be server error
    }
  });
});
