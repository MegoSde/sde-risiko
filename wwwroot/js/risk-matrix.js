"use strict";
RiskApp.run(async () => {
    const A = RiskApp, matrix = A.$("#matrix"), target = A.$("#risk-list"), details = A.$("#risk-details");
    let rows = [], levels, filter = null, selected = null;
    const level = score => score >= levels.criticalMin ? "Critical" : score >= levels.highMin ? "High" : score > levels.lowMax ? "Medium" : "Low";
    function render() {
        matrix.innerHTML = '<table class="matrix"><caption>Scenario Likelihood → / Consequence Index ↑</caption><thead><tr><th scope="col">Consequence Index / Likelihood</th>' + [1,2,3,4,5].map(x => `<th scope="col">${x}</th>`).join("") + '</tr></thead><tbody>' + [5,4,3,2,1].map(y => `<tr><th scope="row">${y}</th>${[1,2,3,4,5].map(x => {
            const count = rows.filter(r => r.consequenceIndex === y && r.scenarioLikelihood === x).length;
            return `<td class="${level(x*y).toLowerCase()}"><button type="button" data-x="${x}" data-y="${y}" aria-label="Consequence Index ${y}, Scenario Likelihood ${x}: ${count} scenarier" aria-pressed="${filter?.x === x && filter?.y === y}">${count}</button></td>`;
        }).join("")}</tr>`).join("") + '</tbody></table>';
        const visible = filter ? rows.filter(r => r.consequenceIndex === filter.y && r.scenarioLikelihood === filter.x) : rows;
        target.innerHTML = A.riskScenarioTable(visible, true, selected);
        A.$("#filter-label").textContent = filter ? `Consequence Index ${filter.y} · Scenario Likelihood ${filter.x}` : "Alle scenarier";
        A.$("#risk-count").textContent = `${visible.length} af ${rows.length} risikoscenarier`;
        const row = visible.find(r => r.id === selected);
        details.hidden = !row; details.innerHTML = row ? A.riskScenarioDetails(row) : "";
    }
    async function load() {
        const button = A.$("#refresh-risks"); button.disabled = true;
        try { [rows, levels] = await Promise.all([A.api("risks"), A.api("risk-levels")]); render(); A.feedback(""); }
        finally { button.disabled = false; }
    }
    A.bindRiskScenarios(target, details, id => rows.find(row => row.id === id), id => { selected = id; });
    matrix.addEventListener("click", event => {
        const cell = event.target.closest("[data-x]"); if (!cell) return;
        filter = { x: Number(cell.dataset.x), y: Number(cell.dataset.y) }; selected = null; render();
    });
    A.$("#clear-filter").addEventListener("click", () => { filter = null; selected = null; render(); });
    A.$("#refresh-risks").addEventListener("click", () => A.run(load));
    await load();
});
