"use strict";
RiskApp.run(async () => {
    const A = RiskApp, target = A.$("#risk-list"), details = A.$("#risk-details");
    let rows = [], selected = null, generation = 0;
    async function load() {
        const current = ++generation;
        const button = A.$("#refresh-risks"); button.disabled = true;
        try {
            const data = await A.api("risks");
            if (current !== generation) return;
            rows = data; target.innerHTML = A.riskScenarioTable(rows, false, selected);
            A.$("#risk-count").textContent = `${rows.length} risikoscenarier`;
            const row = rows.find(r => r.id === selected);
            details.hidden = !row; details.innerHTML = row ? A.riskScenarioDetails(row) : "";
            A.feedback("");
        } finally { if (current === generation) button.disabled = false; }
    }
    A.bindRiskScenarios(target, details, id => rows.find(row => row.id === id), id => { selected = id; });
    A.$("#refresh-risks").addEventListener("click", () => A.run(load));
    await load();
});
