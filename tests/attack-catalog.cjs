const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.argv[2] || 'http://127.0.0.1:7081';
(async () => {
    const browser = await chromium.launch({ headless:true }), page = await browser.newPage();
    let system;
    const errors = []; page.on('pageerror',e=>errors.push(e.message));
    async function api(path,method='GET',data) {
        const r=await page.request.fetch(`${base}/api/${path}`,{method,data}); assert.ok(r.ok(),await r.text()); return r.status()===204 ? null : r.json();
    }
    try {
        system=await api('system-assets','POST',{name:'Catalog Browser Host',assetType:'Host'});
        await api('attack-surface','POST',{systemAssetId:system.id,isExposed:true,exposureType:'Internet'});
        let catalogRequests=0;
        page.on('request',r=>{if(r.url().includes('/api/attack-techniques')) catalogRequests++;});
        await page.goto(`${base}/threat-analysis`); await page.waitForLoadState('networkidle');
        assert.equal(catalogRequests,0,'Catalog is not preloaded');
        const form=page.locator('#start-component-form'), search=form.locator('.component-picker input'), results=form.locator('.technique-results');
        await form.locator('[name=systemAssetId]').selectOption(String(system.id));
        await search.fill('phishing'); await results.getByRole('button',{name:/T1566\.001 –/}).waitFor();
        assert.ok(await results.locator('button').count()<=25);
        await results.getByRole('button',{name:/T1566\.001 –/}).click();
        assert.match(await form.locator('[data-selected]').innerText(),/Initial Access/);
        await search.fill('Invented technique'); assert.equal(await search.evaluate(e=>e.checkValidity()),false);
        let posts=0; page.on('request',r=>{if(r.url().endsWith('/api/attack-components')&&r.method()==='POST') posts++;});
        await form.locator('[type=submit]').click(); assert.equal(posts,0);
        await search.fill('T1566.001'); await results.getByRole('button',{name:/T1566\.001 –/}).click();
        const response=page.waitForResponse(r=>r.url().endsWith('/api/attack-components')&&r.request().method()==='POST');
        await form.locator('[type=submit]').click(); const created=await (await response).json();
        const c=created.component;
        await page.reload(); await page.waitForLoadState('networkidle');
        await page.locator(`#component-list [data-select="${c.id}"]`).click();
        await page.locator(`#component-assessment[data-component="${c.id}"]`).waitFor();
        assert.match(await page.locator('#selected-component h2').first().innerText(),/T1566\.001/);
        await page.goto(`${base}/controls`); await page.waitForLoadState('networkidle');
        assert.match(await page.locator('#control-list').innerText(),/T1566\.001/);
        assert.deepEqual(errors,[]);
        console.log('PASS: bounded remote catalog search, phishing/subtechnique selection, arbitrary input rejected, existing component outside first 25 techniques loads, Controls resolves selected IDs.');
    } finally {if(system) await api(`system-assets/${system.id}`,'DELETE');await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});
