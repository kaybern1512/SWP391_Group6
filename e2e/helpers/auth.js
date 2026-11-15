// @ts-check

/**
 * Log in as a given user via the web form.
 * @param {import('@playwright/test').Page} page
 * @param {string} email
 * @param {string} password
 */
async function loginAs(page, email, password = process.env.DEMO_PASSWORD || 'DentalCare@2026!') {
  const emailMap = {
    'patient1@dentalcare.com': 'patient@dentalcare.test',
    'patient@dentalcare.com': 'patient@dentalcare.test',
    'patient1@dentalcare.test': 'patient@dentalcare.test',
    'receptionist@dentalcare.com': 'receptionist@dentalcare.test',
    'receptionist1@dentalcare.com': 'receptionist@dentalcare.test',
    'receptionist1@dentalcare.test': 'receptionist@dentalcare.test',
    'dentist@dentalcare.com': 'dentist1@dentalcare.test',
    'dentist1@dentalcare.com': 'dentist1@dentalcare.test',
    'dentist@dentalcare.test': 'dentist1@dentalcare.test',
    'dentist2@dentalcare.com': 'dentist2@dentalcare.test',
    'admin@dentalcare.com': 'admin@dentalcare.test'
  };
  const resolvedEmail = emailMap[email.toLowerCase()] || email;

  await page.context().clearCookies();
  await page.goto('/Account/Login');
  await page.waitForLoadState('domcontentloaded');

  // Fill credentials
  await page.locator('input[name="Email"]').fill(resolvedEmail);
  await page.locator('input[name="Password"]').fill(password);

  // Click login button and wait for redirect away from login page
  await page.locator('button.btn-dc-primary').click();
  await page.waitForURL(url => !url.pathname.toLowerCase().includes('/account/login'), { waitUntil: 'domcontentloaded', timeout: 20000 });
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
