const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.argv[2] || 'http://127.0.0.1:7081';
(async () => {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();
    const ids = [], errors = [];
    page.on('pageerror', e => errors.push(e.message));
    async function api(path, method = 'GET', data) {
        const r = await page.request.fetch(base + '/api/' + path, { method, data });
        assert.ok(r.ok(), await r.text());
        return r.status() === 204 ? null : r.json();
    }
    async function create(name, assetType) {
        const asset = await api('system-assets', 'POST', { name, assetType }); ids.push(asset.id); return asset;
    }
    try {
        const network = await create('Surface Network', 'Network'), host = await create('Surface Host', 'Host'), app = await create('Surface App', 'Application');
        await api('system-asset-relations', 'POST', { sourceSystemAssetId: host.id, targetSystemAssetId: network.id, relationType: 'LocatedIn' });
        await api('system-asset-relations', 'POST', { sourceSystemAssetId: app.id, targetSystemAssetId: host.id, relationType: 'HostedOn' });
        const beforeAssets = await api('system-assets'), beforeRisks = await api('risks');
        await page.goto(base + '/attack-surface'); await page.waitForLoadState('networkidle');
        await page.evaluate(() => window.surfaceSentinel = 'same-document');
        const card = page.locator(`[data-asset="${host.id}"]`), checkbox = card.locator('[name=isExposed]');
        const wait = () => page.waitForResponse(r => r.url().includes('/api/attack-surface') && ['POST', 'PUT'].includes(r.request().method()));
        let saved = wait(); await checkbox.check(); assert.equal((await saved).status(), 201);
        saved = wait(); await card.locator('select').selectOption('Internet'); assert.equal((await saved).status(), 200);
        await card.locator('select.unsaved').waitFor({ state: 'detached' });
        const rows = await api('attack-surface');
        assert.equal(Number(await page.locator('#surface-selected').textContent()), rows.filter(e => e.isExposed).length);
        assert.equal(Number(await page.locator('#surface-summary div').filter({ hasText: 'Internet exposed' }).locator('dd').textContent()), rows.filter(e => e.isExposed && e.exposureType === 'Internet').length);
        await card.locator('textarea').fill('Public web server\nWith a second line');
        await assert.doesNotReject(() => card.locator('textarea.unsaved').waitFor());
        assert.equal(await card.locator('textarea').evaluate(e => getComputedStyle(e).resize), 'none');
        saved = wait(); await card.locator('textarea').blur(); await saved;
        await card.locator('textarea.unsaved').waitFor({ state: 'detached' });
        await page.locator('#surface-type').selectOption('Application'); assert.equal(await card.isVisible(), false);
        await page.locator('#surface-type').selectOption(''); await page.locator('#surface-network').selectOption(String(network.id));
        assert.equal(await card.isVisible(), true); assert.equal(await page.locator(`[data-asset="${app.id}"]`).isVisible(), false);
        await page.locator('#surface-network').selectOption(''); await page.locator('#surface-only').check();
        saved = wait(); await checkbox.uncheck(); await saved; await card.waitFor({ state: 'hidden' });
        await page.locator('#surface-only').uncheck();
        saved = wait(); await checkbox.check(); await saved;
        assert.equal(await card.locator('textarea').inputValue(), 'Public web server\nWith a second line');
        // A rejected save stays visibly dirty and can be retried without navigation.
        await page.route('**/api/attack-surface/*', route => route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ error: 'Test failure' }) }), { times: 1 });
        saved = wait(); await card.locator('select').selectOption('RemoteAccess'); assert.equal((await saved).status(), 400);
        await card.locator('[data-retry]').waitFor(); assert.ok(await card.locator('select').evaluate(e => e.classList.contains('unsaved')));
        saved = wait(); await card.locator('[data-retry]').click(); await saved;
        await card.locator('[data-retry]').waitFor({ state: 'hidden' });
        assert.equal(await page.evaluate(() => window.surfaceSentinel), 'same-document');
        assert.deepEqual(await api('system-assets'), beforeAssets); assert.deepEqual(await api('risks'), beforeRisks);
        const entries = await api(`attack-surface?systemAssetId=${host.id}`); assert.equal(entries.length, 1); assert.equal(entries[0].exposureType, 'RemoteAccess');
        await page.reload(); await page.waitForLoadState('networkidle'); assert.equal(await checkbox.isChecked(), true);
        await api(`system-assets/${host.id}`, 'DELETE'); ids.splice(ids.indexOf(host.id), 1);
        assert.deepEqual(await api(`attack-surface?systemAssetId=${host.id}`), []);
        assert.deepEqual(errors, []);
        console.log('PASS: Attack Surface create/edit/uncheck/recheck, dirty fields, retry, direct placement/type/selection filters, no reload, reload persistence, cascade and unchanged assets/risks.');
    } finally {
        for (const id of ids.reverse()) await api(`system-assets/${id}`, 'DELETE');
        await browser.close();
    }
})().catch(error => { console.error(error); process.exit(1); });
