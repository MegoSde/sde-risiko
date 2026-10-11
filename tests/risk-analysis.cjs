const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.argv[2] || 'http://127.0.0.1:7081';
(async () => {
    const browser = await chromium.launch({headless:true});
    const page = await browser.newPage({viewport:{width:1440,height:1000}});
    const systemIds=[],processIds=[],businessIds=[],errors=[];
    page.on('pageerror',e=>errors.push(e.message));
    async function api(path,method='GET',data) {
        const r=await page.request.fetch(`${base}/api/${path}`,{method,data});assert.ok(r.ok(),await r.text());return r.status()===204?null:r.json();
    }
    try {
        const t1190=(await api('attack-techniques?search=T1190'))[0],t1078=(await api('attack-techniques?search=T1078'))[0],t1059=(await api('attack-techniques?search=T1059'))[0];
        async function system(name) {const s=await api('system-assets','POST',{name,assetType:'Host'});systemIds.push(s.id);return s;}
        async function process(name,n) {const p=await api('business-processes','POST',{name,confidentiality:n,integrity:n,availability:n});processIds.push(p.id);return p;}
        async function business(name,processes) {
            let a;
            for (const p of processes) a=await api('business-assets','POST',{name,businessProcessId:p.id});
            businessIds.push(a.id);return a;
        }
        const root=await system('Risk UI WEB01'),portal=await system('Risk UI Portal'),db=await system('Risk UI PostgreSQL'),data=await system('Risk UI Data');
        const clinical=await process('Risk UI Clinical',3),research=await process('Risk UI Research',2);
        const shared=await business('Risk UI Shared Data',[clinical,research]),patient=await business('Risk UI Patient Identity',[clinical]);
        await business('Risk UI Unreached',[clinical]);
        for (const s of [root,portal]) await api('attack-surface','POST',{systemAssetId:s.id,isExposed:true,exposureType:'Internet'});
        const start=async(s,t,p)=>(await api('attack-components','POST',{systemAssetId:s.id,attackTechniqueId:t.id,likelihood:p})).component;
        const ca=await start(root,t1190,4),cb=await start(portal,t1190,2),solo=await start(root,t1059,5);
        const edge=async(a,b)=>api('system-asset-relations','POST',{sourceSystemAssetId:a.id,targetSystemAssetId:b.id,relationType:'ConnectsTo'});
        const ra=await edge(root,db),rb=await edge(portal,db),rd=await edge(db,data),back=await edge(db,root);
        const next=async(c,r,t,p)=>(await api(`attack-components/${c.id}/next`,'POST',{systemAssetRelationId:r.id,attackTechniqueId:t.id,likelihood:p})).component;
        const cc=await next(ca,ra,t1078,3);await next(cb,rb,t1078,5);await next(cc,rd,t1059,4);await next(cc,back,t1190,5);
        for (const [a,s] of [[shared,db],[shared,data],[patient,root]]) await api('business-asset-relations','POST',{businessAssetId:a.id,systemAssetId:s.id,relationType:'StoredIn'});
        const baseline=await api('risks');const own=baseline.filter(r=>[ca.id,cb.id,solo.id].includes(r.id));assert.equal(own.length,3);
        const first=own.find(r=>r.id===ca.id);assert.equal(first.scenarioLikelihood,3);assert.equal(first.accumulatedConsequence,13);
        const cells=new Map();for(const r of baseline){const key=`${r.scenarioLikelihood},${r.consequenceIndex}`;cells.set(key,(cells.get(key)||0)+1);}
        const dashboard=await api('dashboard');
        assert.equal(dashboard.riskScenarios,baseline.length);
        for(const level of dashboard.riskLevels) assert.equal(level.count,baseline.filter(r=>r.riskLevel===level.level).length);
        assert.deepEqual(dashboard.topRisks.map(r=>r.id),[...baseline].sort((a,b)=>b.riskScore-a.riskScore||a.id-b.id).slice(0,5).map(r=>r.id));
        const exposed=(await api('attack-surface')).filter(e=>e.isExposed);
        assert.equal(dashboard.attackSurface,exposed.length);
        assert.equal(dashboard.unassessedEntryPoints,exposed.filter(e=>!baseline.some(r=>r.initialAttackComponent.systemAssetId===e.systemAssetId)).length);
        await page.goto(`${base}/dashboard`);await page.waitForLoadState('networkidle');
        assert.equal(await page.locator('#stats article').count(),7);
        assert.equal(await page.locator('#risk-levels article').count(),4);
        assert.equal(await page.locator('#top-risks tbody tr').count(),dashboard.topRisks.length);
        await page.evaluate(()=>window.dashboardSentinel='same-document');
        await page.locator('#top-risks button[data-risk]').first().click();
        assert.match(await page.locator('#risk-details').innerText(),/Scenario Likelihood/);
        await page.getByRole('button',{name:'Opdatér overblik'}).click();
        await page.waitForLoadState('networkidle');
        assert.equal(await page.evaluate(()=>window.dashboardSentinel),'same-document');
        await page.goto(`${base}/risk-analysis`);await page.waitForLoadState('networkidle');
        assert.equal(await page.locator('#risk-list tbody tr').count(),baseline.length);
        assert.equal(await page.locator('main input, main select, main textarea').count(),0);
        await page.evaluate(()=>window.riskSentinel='same-document');
        await page.locator(`#risk-list tr[data-risk="${ca.id}"] td`).nth(2).click();
        await page.locator('#risk-details').waitFor();
        assert.match(await page.locator('#risk-details').innerText(),/PathLikelihood = MIN\(4, 3/);
        assert.match(await page.locator('#risk-details').innerText(),/Scenario Likelihood/);
        assert.match(await page.locator('#risk-details').innerText(),/cycle: path stoppet/);
        assert.equal(await page.locator('#risk-details tbody tr').filter({hasText:'Risk UI Shared Data'}).count(),1);
        assert.match(await page.locator('#risk-details tbody tr').filter({hasText:'Risk UI Shared Data'}).innerText(),/5 \+ 3 = 8/);
        assert.equal(await page.evaluate(()=>window.riskSentinel),'same-document');
        await page.goto(`${base}/risk-matrix`);await page.waitForLoadState('networkidle');
        await page.evaluate(()=>window.matrixSentinel='same-document');
        assert.equal(await page.locator('#matrix button[data-x]').count(),25);
        let total=0;
        for(let y=1;y<=5;y++)for(let x=1;x<=5;x++) {
            const cell=page.locator(`#matrix [data-x="${x}"][data-y="${y}"]`);
            const count=Number(await cell.innerText());total+=count;assert.equal(count,cells.get(`${x},${y}`)||0);
            await cell.click();
            const expected=baseline.filter(r=>r.scenarioLikelihood===x&&r.consequenceIndex===y).map(r=>String(r.id)).sort();
            const actual=await page.locator('#risk-list tbody tr').evaluateAll(rows=>rows.map(r=>r.dataset.risk).sort());
            assert.deepEqual(actual,expected);
        }
        assert.equal(total,baseline.length);
        const selectedCell=page.locator(`#matrix [data-x="${first.scenarioLikelihood}"][data-y="${first.consequenceIndex}"]`);
        await selectedCell.click();await page.locator(`#risk-list button[data-risk="${ca.id}"]`).click();
        assert.match(await page.locator('#risk-details').innerText(),/Risk UI WEB01/);
        assert.equal(await page.evaluate(()=>window.matrixSentinel),'same-document');
        await page.getByRole('button',{name:'Vis alle scenarier'}).click();
        assert.equal(await page.locator('#risk-list tbody tr').count(),baseline.length);
        // Read-only refresh reflects edited graph assessments, with no page reload.
        await api(`attack-components/${cc.id}`,'PUT',{likelihood:4,comment:'Updated bottleneck'});
        await page.getByRole('button',{name:'Opdatér analyse'}).click();await page.waitForLoadState('networkidle');
        const updated=await api('risks');assert.equal(updated.find(r=>r.id===ca.id).scenarioLikelihood,4);
        const refreshedRow=page.locator(`#risk-list tr[data-risk="${ca.id}"]`);assert.equal(await refreshedRow.locator('td').nth(3).innerText(),'4');
        assert.equal(await page.evaluate(()=>window.matrixSentinel),'same-document');
        await page.setViewportSize({width:390,height:844});
        assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),'Mobile matrix overflows');
        await page.goto(`${base}/risk-analysis`);await page.waitForLoadState('networkidle');
        assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),'Mobile risk analysis overflows');
        assert.equal(await page.locator('#feedback.error').count(),0);assert.deepEqual(errors,[]);
        console.log('PASS: Risk Analysis rows/read-only details, shared asset/process sums, cycle explanation, all 25 matrix cells/filter memberships, exact-one-cell scenario placement, refresh after model edit, no page reload and mobile layout.');
    } finally {
        for(const id of systemIds.reverse()) await api(`system-assets/${id}`,'DELETE');
        for(const id of businessIds) await api(`business-assets/${id}`,'DELETE');
        for(const id of processIds) await api(`business-processes/${id}`,'DELETE');
        await browser.close();
    }
})().catch(e=>{console.error(e);process.exit(1);});
