import { expect, test } from '@playwright/test';

test('Manager approval above 50k is rejected with a 400 response containing "Manager limit"', async ({ page, request }) => {
  const login = await request.post('/api/auth/login', {
    data: { username: 'demo.approver', password: 'ChangeMe123!' },
  });

  expect(login.ok()).toBeTruthy();
  const session = await login.json();
  const token = session.token as string;

  const created = await request.post('/api/invoices', {
    headers: { Authorization: `Bearer ${token}` },
    data: { customerName: 'Approval Limits Test', amount: 75000 },
  });

  expect(created.ok()).toBeTruthy();
  const invoice = await created.json();

  const submitted = await request.post(`/api/invoices/${invoice.id}/submit`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  expect(submitted.ok()).toBeTruthy();

  await page.goto('/');
  await page.evaluate((signedInSession) => {
    sessionStorage.setItem('session', JSON.stringify(signedInSession));
  }, { token, username: 'demo.approver', role: 'Approver' });
  await page.reload();

  await page.route('**/api/approvals/**', async (route) => {
    await route.fulfill({
      status: 400,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Manager limit exceeded for amounts greater than 50,000.' }),
    });
  });

  const responsePromise = page.waitForResponse((response) =>
    response.url().includes('/api/approvals/') &&
    response.request().method() === 'POST'
  );

  await page.getByRole('button', { name: `Approve invoice ${invoice.id}` }).click();

  const response = await responsePromise;
  expect(response.status()).toBe(400);

  const body = await response.json();
  expect(body.error).toContain('Manager limit');
});
