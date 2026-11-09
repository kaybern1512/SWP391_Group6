// @ts-check

/**
 * Log in as a given user via the web form.
 * @param {import('@playwright/test').Page} page
 * @param {string} email
 * @param {string} password
 */
async function loginAs(page, email, password = 'Password123@') {
  await page.context().clearCookies();
  await page.goto('/Account/Login');
  await page.waitForLoadState('domcontentloaded');

  // Fill credentials
  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill(password);

  // Click login button and wait for redirect away from login page
  await page.locator('button.btn-dc-primary').click();
  await page.waitForURL(url => !url.pathname.toLowerCase().includes('/account/login'), { timeout: 15000 });
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
        !text.includes('cdn.jsdelivr.net') &&
        !text.includes('SignalR') &&
        !text.includes('negotiate') &&
        !text.includes('Failed to load resource')
      ) {
        errorList.push(`[CONSOLE_ERROR] ${text}`);
      }
    }
  });
}

module.exports = { loginAs, capturePageErrors };
