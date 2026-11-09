// @ts-check

/**
 * Log in as a given user via the web form.
 * @param {import('@playwright/test').Page} page
 * @param {string} email
 * @param {string} password
 */
async function loginAs(page, email, password = 'Password123@') {
  await page.goto('/Account/Login');
  await page.waitForLoadState('domcontentloaded');

  // Fill credentials
  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill(password);

  // Click login button (the primary submit button)
  await Promise.all([
    page.waitForNavigation({ timeout: 15000 }),
    page.locator('button.btn-dc-primary').click(),
  ]);
}

/**
 * Attach listeners to capture console errors and unhandled exceptions.
 * @param {import('@playwright/test').Page} page
 * @param {string[]} errorList
 */
function capturePageErrors(page, errorList) {
  page.on('pageerror', (err) => {
    errorList.push(`[PAGE_ERROR] ${err.message}`);
  });
  page.on('console', (msg) => {
    if (msg.type() === 'error') {
      const text = msg.text();
      // Ignore external CDN image 404s, font 404s, and SignalR reconnect notices
      if (
        !text.includes('favicon') &&
        !text.includes('fonts.gstatic.com') &&
        !text.includes('cloudinary') &&
        !text.includes('Failed to load resource') &&
        !text.includes('signalr')
      ) {
        errorList.push(`[CONSOLE_ERROR] ${text}`);
      }
    }
  });
}

module.exports = {
  loginAs,
  capturePageErrors,
};
