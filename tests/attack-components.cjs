const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.argv[2] || 'http://127.0.0.1:7081';
(async () => {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
    const ids = [], errors = [];
    page.on('pageerror', e => errors.push(e.message)); page.on('dialog', d => d.accept());
    async function api(path, method='GET', data) {
        const response = await page.request.fetch(`${base}/api/${path}`, { method, data });
        assert.ok(response.ok(), await response.text()); return response.status() === 204 ? null : response.json();
    }
    async function asset(name, assetType) { const a = await api('system-assets','POST',{name,assetType}); ids.push(a.id); return a; }
    async function technique(form, id) {
        await form.locator('.component-picker input').fill(id);
        await form.locator('.technique-results button').filter({hasText: `${id} –`}).click();
    }
    async function save(form, method, suffix) {
        const response = page.waitForResponse(r => r.url().endsWith(suffix) && r.request().method() === method);
        await form.locator('[type=submit]').click(); const r = await response;
        assert.ok(r.ok(), await r.text()); return r.json();
    }
    async function selected(id) { await page.locator(`#component-assessment[data-component="${id}"]`).waitFor(); }
    try {
        const a = await asset('Component WEB01','Host'), b = await asset('Component ResearchPortal','Application'), db = await asset('Component PostgreSQL','Service'), incoming = await asset('Component IncomingOnly','Network');
        const ra = await api('system-asset-relations','POST',{sourceSystemAssetId:a.id,targetSystemAssetId:db.id,relationType:'ConnectsTo'});
        const rb = await api('system-asset-relations','POST',{sourceSystemAssetId:b.id,targetSystemAssetId:db.id,relationType:'Uses'});
        await api('system-asset-relations','POST',{sourceSystemAssetId:incoming.id,targetSystemAssetId:a.id,relationType:'Uses'});
        for (const item of [a,b]) await api('attack-surface','POST',{systemAssetId:item.id,isExposed:true,exposureType:'Internet'});
        await page.goto(`${base}/threat-analysis`); await page.waitForLoadState('networkidle');
        assert.equal(await page.getByRole('link',{name:'Systemanalyse',exact:true}).count(),0);
        assert.equal((await page.request.get(`${base}/system-analysis`)).status(),404);
        await page.evaluate(() => window.componentSentinel='same-document');
        const start = page.locator('#start-component-form');
        assert.equal(await start.locator(`option[value="${db.id}"]`).count(),0);
        await start.locator('[name=systemAssetId]').selectOption(String(a.id));
        await technique(start,'T1190'); await start.locator('[name=likelihood]').fill('2'); await start.locator('[name=comment]').fill('First entry');
        const ca = (await save(start,'POST','/api/attack-components')).component; await selected(ca.id);
        let next = page.locator(`.next-component-form[data-relation="${ra.id}"]`);
        assert.equal(await page.locator(`.next-component-form[data-target="${incoming.id}"]`).count(),0);
        await technique(next,'T1078'); await next.locator('[name=likelihood]').fill('3'); await next.locator('[name=comment]').fill('One shared assessment');
        const ct = (await save(next,'POST',`/api/attack-components/${ca.id}/next`)).component; await selected(ct.id);
        await start.locator('[name=systemAssetId]').selectOption(String(b.id));
        await start.locator('[name=likelihood]').fill('4'); await start.locator('[name=comment]').fill('Second entry');
        const cb = (await save(start,'POST','/api/attack-components')).component; await selected(cb.id);
        next = page.locator(`.next-component-form[data-relation="${rb.id}"]`);
        await next.locator('..').getByRole('button',{name:/Brug T1078/}).click();
        assert.equal(await next.locator('[name=likelihood]').isDisabled(),true);
        assert.equal(await next.locator('[name=comment]').inputValue(),'One shared assessment');
        const reused = await save(next,'POST',`/api/attack-components/${cb.id}/next`);
        assert.equal(reused.componentCreated,false); assert.equal(reused.component.id,ct.id); await selected(ct.id);
        assert.match(await page.locator('#selected-component').innerText(),/Component WEB01/);
        assert.match(await page.locator('#selected-component').innerText(),/Component ResearchPortal/);
        assert.equal((await api(`attack-connections?toAttackComponentId=${ct.id}`)).length,2);
        assert.equal((await api('attack-components')).filter(c=>c.systemAssetId===db.id&&c.attackTechniqueId===ct.attackTechniqueId).length,1);
        const assessment = page.locator('#component-assessment');
        await assessment.locator('[name=likelihood]').fill('4'); await assessment.locator('[name=comment]').fill('Updated shared assessment\nSecond line');
        assert.equal(await assessment.locator('[name=comment]').evaluate(e=>getComputedStyle(e).resize),'none');
        assert.equal(await assessment.locator('.unsaved').count(),2);
        await save(assessment,'PUT',`/api/attack-components/${ct.id}`); await selected(ct.id);
        // An arbitrary search string is never an ATT&CK selection.
        await start.locator('.component-picker input').fill('Invented technique');
        assert.equal(await start.locator('.component-picker input').evaluate(e=>e.checkValidity()),false);
        await start.locator('.component-picker input').fill('phishing');
        await start.locator('.technique-results button').filter({hasText:'T1566.001 –'}).waitFor();
        assert.ok(await start.locator('.technique-results button').count()<=25);
        assert.equal(await page.evaluate(()=>window.componentSentinel),'same-document');
        await page.reload(); await page.waitForLoadState('networkidle');
        await page.locator(`#component-list [data-select="${ct.id}"]`).click(); await selected(ct.id);
        assert.equal(await page.locator('#component-assessment [name=likelihood]').inputValue(),'4');
        assert.equal((await api(`attack-connections?toAttackComponentId=${ct.id}`)).length,2);
        const pending = page.waitForResponse(r=>r.url().includes('/api/attack-connections/')&&r.request().method()==='DELETE');
        await page.locator('#selected-component [data-delete-connection]').first().click(); await pending;
        await page.waitForLoadState('networkidle'); assert.equal((await api(`attack-connections?toAttackComponentId=${ct.id}`)).length,1);
        await page.goto(`${base}/controls`); await page.waitForLoadState('networkidle');
        const controlA = page.locator(`[data-attack="${ca.id}"]`), controlB = page.locator(`[data-attack="${cb.id}"]`);
        assert.match(await controlA.locator('[data-selection]').innerText(), /CIS 7, CIS 16, CIS 18/);
        await controlA.locator('summary').click(); assert.equal(await controlA.evaluate(e => e.open), true);
        await controlB.locator('summary').click();
        assert.equal(await controlA.evaluate(e => e.open), false);
        assert.equal(await page.locator('#control-list details[open]').count(), 1);
        await controlA.locator('summary').click();
        const controlSaved = page.waitForResponse(r => r.url().endsWith(`/api/component-controls/${ca.id}/1`) && r.request().method() === 'PUT');
        await controlA.locator('[data-control="1"][type=checkbox]').check();
        await controlA.locator('summary').click(); await controlSaved;
        await page.waitForFunction(id => document.querySelector(`[data-selection="${id}"]`).textContent.includes('CIS 1, '), ca.id);
        assert.equal(await controlA.evaluate(e => e.open), false);
        assert.equal(await page.locator('#control-list details[open]').count(), 0);
        await page.reload(); await page.waitForLoadState('networkidle');
        assert.match(await controlA.locator('[data-selection]').innerText(), /CIS 1, CIS 7, CIS 16, CIS 18/);
        await controlA.locator('summary').click();
        const unchecked = page.waitForResponse(r => r.url().endsWith(`/api/component-controls/${ca.id}/1`) && r.request().method() === 'PUT');
        await controlA.locator('[data-control="1"][type=checkbox]').uncheck(); await unchecked;
        await controlA.locator('summary').click();
        await page.waitForFunction(id => !document.querySelector(`[data-selection="${id}"]`).textContent.includes('CIS 1, '), ca.id);
        for (const route of ['controls','risk-analysis','risk-matrix','cis-priorities','report','dashboard']) {
            await page.goto(`${base}/${route}`); await page.waitForLoadState('networkidle'); assert.equal(await page.locator('#feedback.error').count(),0);
        }
        assert.deepEqual(errors,[]);
        console.log('PASS: removed System Analysis, surface-only starts, catalog autocomplete, directed next components, shared assessment reuse, two incoming connections, edit/delete without reload, restart-page persistence, exclusive CIS panels with saved summaries and remaining pages.');
    } finally { for (const id of ids.reverse()) await api(`system-assets/${id}`,'DELETE'); await browser.close(); }
})().catch(error=>{console.error(error);process.exit(1);});
