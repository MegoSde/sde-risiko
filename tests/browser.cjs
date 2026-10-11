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
    try {
        await goto("business-analysis");
        const form = page.locator("#process-form");
        await form.locator("[name=name]").fill("Browser klinisk behandling");
        await form.locator("[name=confidentiality]").fill("3");
        await form.locator("[name=integrity]").fill("3");
        await form.locator("[name=availability]").fill("2");
        assert.equal(await page.locator("#consequence").inputValue(), "4,5");
        const process = await save("#process-form", "business-processes");
        processIds.push(process.id);
        await page
            .locator("#asset-form [name=name]")
            .fill("Browser patientdata");
        await page
            .locator("#asset-form select")
            .selectOption(String(process.id));
        await save("#asset-form", "business-assets");
        await goto("asset-mapping");
        await page.locator("#system-form [name=name]").fill("Browser WEB01");
        await page.locator("#system-form [name=blastRadius]").fill("2.5");
        await page
            .locator("#system-form [name=blastRadiusReason]")
            .fill("Adgang til kliniske data");
        const system = await save("#system-form", "system-assets");
        systemIds.push(system.id);
        await page
            .locator("#mapping-list")
            .getByLabel("Browser WEB01 · Server")
            .check();
        await page.waitForLoadState("networkidle");
        await goto("system-analysis");
        const section = page
            .locator("#system-list section")
            .filter({
                has: page.getByRole("heading", {
                    name: "Browser WEB01",
                    exact: true,
                }),
            });
        assert.match(await section.innerText(), /Direkte konsekvens: 4,5/);
        await section
            .getByRole("button", { name: "Rediger", exact: true })
            .click();
        await page.locator("#system-form [name=blastRadius]").fill("4");
        await page
            .locator("#system-form [name=blastRadiusReason]")
            .fill("Adgang til data og andre systemer");
        await save("#system-form", "system-assets");
        await page.locator("#system-form [name=name]").fill("Browser DB01");
        const target = await save("#system-form", "system-assets");
        systemIds.push(target.id);
        const access = page.locator(
            `[data-source="${system.id}"][data-target="${target.id}"]`,
        );
        await access.check();
        await page.waitForLoadState("networkidle");
        await page
            .locator(`[data-description="${system.id}-${target.id}"]`)
            .fill("Databasekonto");
        await page
            .locator(
                `[data-save-access="${system.id}"][data-target="${target.id}"]`,
            )
            .click();
        await page.waitForLoadState("networkidle");
        await goto("threat-analysis");
        await page
            .locator("[name=systemAssetId]")
            .selectOption(String(system.id));
        const techniques = await data("attack-techniques");
        await page
            .locator("[name=attackTechniqueId]")
            .selectOption(
                String(techniques.find((t) => t.techniqueId === "T1190").id),
            );
        assert.match(
            await page.locator("#technique-info").innerText(),
            /Initial Access/,
        );
        await page
            .locator("[name=scenario]")
            .fill("Browser exploit af webapplikation");
        await page.locator("[name=likelihood]").fill("5");
        const threat = await save("#threat-form", "system-threats");
        await goto("controls");
        await page
            .locator(`[data-threat="${threat.id}"][data-control="7"]`)
            .uncheck();
        await page.waitForLoadState("networkidle");
        await page
            .locator(`[data-threat="${threat.id}"][data-control="6"]`)
            .check();
        await page.waitForLoadState("networkidle");
        await page
            .locator(`[data-comment="${threat.id}-6"]`)
            .fill("Begræns servicekontoens rettigheder");
        await page
            .locator(`[data-save="${threat.id}"][data-control="6"]`)
            .click();
        await page.waitForLoadState("networkidle");
        await goto("risk-analysis");
        const riskRow = page
            .locator("#risk-list tbody tr")
            .filter({ hasText: "Browser WEB01" });
        assert.match(await riskRow.innerText(), /Critical/);
        await riskRow.getByText("Vis grundlag", { exact: true }).click();
        assert.match(await riskRow.innerText(), /Browser klinisk behandling/);
        await goto("risk-matrix");
        await page.locator('[data-x="5"][data-y="4"]').click();
        assert.match(
            await page.locator("#risk-list").innerText(),
            /Browser WEB01/,
        );
        await page.locator('[data-x="1"][data-y="1"]').click();
        assert.match(
            await page.locator("#risk-list").innerText(),
            /Ingen data/,
        );
        await page.getByRole("button", { name: "Vis alle risici" }).click();
        assert.match(
            await page.locator("#risk-list").innerText(),
            /Browser WEB01/,
        );
        await goto("cis-priorities");
        const priorities = await data("cis-priorities");
        assert.equal(
            priorities.find((p) => p.control.number === 6).weightedRiskScore,
            20,
        );
        await goto("dashboard");
        assert.match(await page.locator("#stats").innerText(), /20/);
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
            "PASS: Browser workflow, fetch CRUD, mappings, access, controls, risk details, matrix filtering, priorities, report printing and mobile dashboard.",
        );
    } finally {
        for (const id of systemIds)
            await page.request.delete(`${base}/api/system-assets/${id}`);
        for (const id of processIds)
            await page.request.delete(`${base}/api/business-processes/${id}`);
        await browser.close();
    }
})().catch((error) => {
    console.error(error);
    process.exitCode = 1;
});
