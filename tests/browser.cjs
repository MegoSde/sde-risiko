// Optional browser integration test; requires Playwright and its Chromium browser.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const assert = require("node:assert/strict");
const base = process.argv[2] || "http://127.0.0.1:7070";

(async () => {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage({
        viewport: { width: 1440, height: 1000 },
    });
    const errors = [];
    const processIds = [],
        systemIds = [];
    page.on("pageerror", (error) => errors.push(error.message));
    page.on("console", (message) => {
        if (message.type() === "error") errors.push(message.text());
    });
    page.on("dialog", (dialog) => dialog.accept());
    const data = async (name) =>
        (await page.request.get(`${base}/api/${name}`)).json();
    const goto = async (name) => {
        await page.goto(`${base}/${name}`);
        await page.waitForLoadState("networkidle");
    };
    const save = async (selector, resource) => {
        const response = page.waitForResponse(
            (r) =>
                r.url().includes("/api/" + resource) &&
                ["POST", "PUT"].includes(r.request().method()),
        );
        await page
            .locator(selector)
            .getByRole("button", { name: /^Gem/ })
            .click();
        const result = await response;
        assert.ok(result.ok(), await result.text());
        await page.waitForLoadState("networkidle");
        return result.json();
    };
    const initialAssetIds = new Set((await data("business-assets")).map(asset => asset.id));
    try {
        await goto("business-analysis");
        assert.equal(await page.locator("#process-form, #asset-form").count(), 0);
        const create = page.locator(".add-process");
        await create.fill("Browser klinisk behandling");
        const createdResponse = page.waitForResponse(r => r.url().endsWith("/api/business-processes") && r.request().method() === "POST");
        await create.press("Enter");
        const process = await (await createdResponse).json();
        processIds.push(process.id);
        const row = page.locator(`[data-process="${process.id}"]`);
        await row.waitFor();
        const description = row.locator('[data-field="description"]');
        const shortHeight = await description.evaluate(el => el.clientHeight);
        await description.fill("Linje med procesbeskrivelse\n".repeat(12));
        assert.equal(await description.evaluate(el => getComputedStyle(el).resize), "none");
        assert.ok(await description.evaluate(el => el.clientHeight) > shortHeight);
        assert.ok(await description.evaluate(el => el.scrollHeight <= el.clientHeight + 1));
        await description.fill("");
        assert.equal(await description.evaluate(el => el.classList.contains("unsaved")), false);
        assert.equal(await page.locator(".process-sheet th").nth(4).innerText(), "K");
        assert.match(await page.locator(".process-sheet th").nth(4).getAttribute("title"), /afrundet til nærmeste 0,5/);
        assert.equal(await page.locator(".process-sheet th").last().innerText(), "");
        for (const [field, value] of [["confidentiality", "3"], ["integrity", "3"], ["availability", "2"], ["description", "Redigeret direkte i tabellen"]]) {
            const response = page.waitForResponse(r => r.url().endsWith(`/api/business-processes/${process.id}`) && r.request().method() === "PUT");
            await row.locator(`[data-field="${field}"]`).fill(value);
            assert.ok(await row.locator(`[data-field="${field}"]`).evaluate(el => el.classList.contains("unsaved")));
            await row.locator(`[data-field="${field}"]`).press("Tab");
            assert.ok((await response).ok());
            await page.waitForFunction(({ id, field }) => !document.querySelector(`[data-process="${id}"] [data-field="${field}"]`).classList.contains("unsaved"), { id: process.id, field });
        }
        await page.waitForLoadState("networkidle");
        assert.equal(await row.locator(".consequence").innerText(), "4,5");
        assert.equal(await page.locator("#feedback").innerText(), "");
        assert.equal(await row.getByRole("button", { name: "Rediger", exact: true }).count(), 0);
        const addAsset = row.locator(".add-asset");
        const addTag = async (name) => {
            const response = page.waitForResponse(r => r.url().endsWith("/api/business-assets") && r.request().method() === "POST");
            await addAsset.fill(name);
            await addAsset.press("Enter");
            assert.ok((await response).ok());
            await page.waitForLoadState("networkidle");
        };
        await addTag("Browser patientdata");
        assert.equal(await page.locator('#known-assets option[value="Browser patientdata"]').count(), 1);
        await addAsset.fill(" browser PATIENTDATA ");
        await addAsset.press("Enter");
        await page.waitForLoadState("networkidle");
        assert.equal(await row.locator(".asset-tag").count(), 1);
        await addTag("Browser midlertidigt asset");
        const removeTag = row.getByRole("button", { name: "Fjern Browser midlertidigt asset", exact: true });
        await removeTag.click();
        await removeTag.waitFor({ state: "detached" });
        assert.equal(await row.locator(".asset-tag").count(), 1);
        const secondResponse = page.waitForResponse(r => r.url().endsWith("/api/business-processes") && r.request().method() === "POST");
        await create.fill("Browser ekstra proces");
        await create.press("Enter");
        const second = await (await secondResponse).json();
        processIds.push(second.id);
        const secondRow = page.locator(`[data-process="${second.id}"]`);
        await secondRow.waitFor();
        const knownResponse = page.waitForResponse(r => r.url().endsWith("/api/business-assets") && r.request().method() === "POST");
        await secondRow.locator(".add-asset").fill("browser PATIENTDATA");
        await secondRow.locator(".add-asset").press("Enter");
        const shared = await (await knownResponse).json();
        assert.equal(shared.name, "Browser patientdata");
        assert.deepEqual(shared.businessProcessIds, [process.id, second.id]);
        assert.equal((await data("business-assets")).filter(asset => asset.name === shared.name).length, 1);
        await goto("asset-model");
        const sharedCard = page.locator(`[data-business="${shared.id}"]`);
        assert.match(await sharedCard.innerText(), /Browser klinisk behandling/);
        assert.match(await sharedCard.innerText(), /Browser ekstra proces/);
        await sharedCard.getByRole("button", { name: "Omdøb", exact: true }).click();
        await sharedCard.locator('.asset-name-form [name="name"]').fill("Browser patientdata omdøbt");
        assert.ok(await sharedCard.locator('.asset-name-form [name="name"]').evaluate(el => el.classList.contains("unsaved")));
        const renameResponse = page.waitForResponse(r => r.url().endsWith(`/api/business-assets/${shared.id}`) && r.request().method() === "PUT");
        await sharedCard.getByRole("button", { name: "Gem navn", exact: true }).click();
        assert.ok((await renameResponse).ok());
        await page.waitForFunction(id => document.querySelector(`[data-business="${id}"] h3`)?.textContent === "Browser patientdata omdøbt", shared.id);
        await goto("business-analysis");
        assert.equal(await row.getByRole("button", { name: "Fjern Browser patientdata omdøbt", exact: true }).count(), 1);
        const secondTag = secondRow.getByRole("button", { name: "Fjern Browser patientdata omdøbt", exact: true });
        await secondTag.click();
        await secondTag.waitFor({ state: "detached" });
        assert.equal(await row.locator(".asset-tag").count(), 1);
        assert.equal((await data("business-assets")).find(asset => asset.id === shared.id).name, "Browser patientdata omdøbt");
        await page.waitForLoadState("networkidle");
        await secondRow.getByRole("button", { name: "Slet proces", exact: true }).click();
        await page.waitForLoadState("networkidle");
        await secondRow.waitFor({ state: "detached" });
        await goto("business-analysis");
        assert.equal(await page.locator(`[data-process="${process.id}"] [data-field="description"]`).inputValue(), "Redigeret direkte i tabellen");
        await goto("asset-model");
        assert.equal(await page.locator("h1").innerText(), "Asset Model");
        assert.equal(await page.locator('[name="zone"]').count(), 0);
        const assets = await data("business-assets");
        const asset = assets.find(a => a.name === "Browser patientdata omdøbt" && a.businessProcessIds.includes(process.id));
        const businessCard = page.locator(`[data-business="${asset.id}"]`);
        assert.match(await businessCard.innerText(), /Konsekvens: 4,5/);
        await page.locator("#system-editor > summary").click();
        await page.locator("#system-form [name=name]").fill("Browser WEB01");
        const system = await save("#system-form", "system-assets");
        systemIds.push(system.id);
        await page.locator(`[data-system="${system.id}"]`).waitFor();
        const relationSelector = `[data-business="${asset.id}"] .relation-form`;
        await businessCard.locator(".relation-editor > summary").click();
        await page.locator(relationSelector + " [name=targetId]").selectOption(String(system.id));
        await page.locator(relationSelector + " [name=relationType]").selectOption("DependsOn");
        await save(relationSelector, "business-asset-relations");
        await businessCard.locator(".relation-direction").waitFor();
        assert.match(await businessCard.innerText(), /Browser patientdata omdøbt —DependsOn→ Browser WEB01/);
        await businessCard.getByRole("button", { name: "Rediger relation", exact: true }).click();
        await page.locator(relationSelector + " [name=relationType]").selectOption("UsedBy");
        await page.locator(relationSelector + " [name=relationDescription]").fill("Business relation edited");
        const updatedBusiness = await save(relationSelector, "business-asset-relations");
        assert.equal(updatedBusiness.description, "Business relation edited");
        const deletedBusiness = page.waitForResponse(r => r.url().endsWith(`/api/business-asset-relations/${updatedBusiness.id}`) && r.request().method() === "DELETE");
        await businessCard.getByRole("button", { name: "Slet relation", exact: true }).click();
        assert.ok((await deletedBusiness).ok());
        await businessCard.locator(".relation-direction").waitFor({ state: "detached" });
        await businessCard.locator(".relation-editor > summary").click();
        await page.locator(relationSelector + " [name=targetId]").selectOption(String(system.id));
        await page.locator(relationSelector + " [name=relationType]").selectOption("DependsOn");
        await save(relationSelector, "business-asset-relations");
        await businessCard.locator(".relation-direction").waitFor();
        const systemCard = page.locator(`[data-system="${system.id}"]`);
        await systemCard.locator(":scope > summary").click();
        await systemCard.locator(".relation-editor > summary").click();
        const systemRelationSelector = `[data-system="${system.id}"] .relation-form`;
        await page.locator(systemRelationSelector + " [name=targetId]").selectOption("new");
        await page.locator(systemRelationSelector + " .new-system [name=name]").fill("Browser Inline DB");
        await page.locator(systemRelationSelector + " .new-system [name=assetType]").selectOption("Service");
        await page.locator(systemRelationSelector + " [name=relationType]").selectOption("ConnectsTo");
        const systemRelation = await save(systemRelationSelector, "system-asset-relations");
        systemIds.push(systemRelation.targetSystemAssetId);
        await systemCard.locator(".relation-direction").waitFor();
        assert.match(await systemCard.innerText(), /Browser WEB01 —ConnectsTo→ Browser Inline DB/);
        await systemCard.getByRole("button", { name: "Rediger relation", exact: true }).click();
        await page.locator(systemRelationSelector + " [name=relationType]").selectOption("Uses");
        await page.locator(systemRelationSelector + " [name=relationDescription]").fill("System relation edited");
        const updatedSystem = await save(systemRelationSelector, "system-asset-relations");
        assert.equal(updatedSystem.description, "System relation edited");
        const deletedSystem = page.waitForResponse(r => r.url().endsWith(`/api/system-asset-relations/${updatedSystem.id}`) && r.request().method() === "DELETE");
        await systemCard.getByRole("button", { name: "Slet relation", exact: true }).click();
        assert.ok((await deletedSystem).ok());
        await systemCard.locator(".relation-direction").waitFor({ state: "detached" });
        await systemCard.getByRole("button", { name: "Rediger", exact: true }).click();
        await page.locator("#system-form [name=description]").fill("Edited in Asset Model");
        await save("#system-form", "system-assets");
        for (const name of ["attack-surface", "threat-analysis", "controls", "risk-analysis", "risk-matrix", "cis-priorities", "dashboard"]) await goto(name);
        assert.equal((await page.request.get(`${base}/system-analysis`)).status(), 404);
        await goto("report");
        assert.match(
            await page.locator("#report").innerText(),
            /Browser klinisk behandling/,
        );
        await page.evaluate(() => {
            window.print = () => {
                window.printCalled = true;
            };
        });
        await page.getByRole("button", { name: "Print / Save as PDF" }).click();
        assert.ok(await page.evaluate(() => window.printCalled));
        assert.equal(
            await page.locator("#report details:not([open])").count(),
            0,
        );
        await page.pdf({
            path: "/tmp/alpacamedic-report-check.pdf",
            format: "A4",
        });
        await page.screenshot({
            path: "/tmp/alpacamedic-report-check.png",
            fullPage: true,
        });
        await page.setViewportSize({ width: 390, height: 844 });
        await goto("dashboard");
        assert.ok(
            await page.evaluate(
                () => document.documentElement.scrollWidth <= window.innerWidth,
            ),
            "Mobile dashboard overflows",
        );
        await page.screenshot({
            path: "/tmp/alpacamedic-mobile-check.png",
            fullPage: true,
        });
        assert.deepEqual(errors, []);
        console.log(
            "PASS: Asset Model relation CRUD and inline system creation; remaining workflow pages load, System Analysis removed, report printing and mobile dashboard.",
        );
    } finally {
        for (const id of systemIds)
            await page.request.delete(`${base}/api/system-assets/${id}`);
        for (const id of processIds)
            await page.request.delete(`${base}/api/business-processes/${id}`);
        for (const asset of await data("business-assets"))
            if (!initialAssetIds.has(asset.id) && asset.name.startsWith("Browser "))
                await page.request.delete(`${base}/api/business-assets/${asset.id}`);
        await browser.close();
    }
})().catch((error) => {
    console.error(error);
    process.exitCode = 1;
});
