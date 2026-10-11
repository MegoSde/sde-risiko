"use strict";
RiskApp.run(async () => {
  const A = RiskApp, target = A.$("#top-risks"), details = A.$("#risk-details");
  let rows = [], selected = null;
  A.bindRiskScenarios(target, details, id => rows.find(r => r.id === id), id => { selected = id; });
  async function load() {
    const button = A.$("#refresh-dashboard");
    button.disabled = true;
    try {
      const d = await A.api("dashboard");
      A.$("#stats").innerHTML = [
        ["Forretningsprocesser", d.businessProcesses, "business-analysis"],
        ["Forretningsassets", d.businessAssets, "asset-model"],
        ["Systemassets", d.systemAssets, "asset-model"],
        ["Attack Surface assets", d.attackSurface, "attack-surface"],
        ["Attack Components", d.attackComponents, "threat-analysis"],
        ["Attack Connections", d.attackConnections, "threat-analysis"],
        ["Risikoscenarier", d.riskScenarios, "risk-analysis"],
      ].map(([label, value, page]) => `<article><strong class="stat">${A.number(value)}</strong><a href="/${page}">${label}</a></article>`).join("");
      A.$("#risk-levels").innerHTML = d.riskLevels.map(r => `<article><span class="badge ${r.level.toLowerCase()}">${A.esc(r.level)}</span><strong class="stat">${A.number(r.count)}</strong><span>scenarier</span></article>`).join("");
      A.$("#assessment-status").innerHTML = d.unassessedEntryPoints
        ? `${A.number(d.unassessedEntryPoints)} valgte Attack Surface assets mangler en startkomponent. <a href="/threat-analysis">Vurder startpunkter i Threat Analysis</a>.`
        : d.attackSurface ? "Alle valgte Attack Surface assets har mindst én startkomponent."
        : 'Vælg først angrebspunkter på <a href="/attack-surface">Attack Surface</a>.';
      rows = d.topRisks;
      target.innerHTML = rows.length ? A.riskScenarioTable(rows, true, selected)
        : '<p class="empty">Ingen risikoscenarier endnu. Opret startkomponenter fra Attack Surface i <a href="/threat-analysis">Threat Analysis</a>.</p>';
      const row = rows.find(r => r.id === selected);
      details.hidden = !row; details.innerHTML = row ? A.riskScenarioDetails(row) : "";
      const top = d.priorities.filter(p => p.findings > 0).sort((a, b) => b.findings - a.findings).slice(0, 5);
      A.$("#common-controls").innerHTML = top.length ? A.table(["CIS Control", "Navn", "Komponenter"], top.map(p => ["CIS " + p.control.number, A.esc(p.control.name), p.findings]))
        : '<p class="empty">Ingen CIS Controls valgt endnu. Vælg kontroller på <a href="/controls">Controls</a>.</p>';
      A.feedback("");
    } finally { button.disabled = false; }
  }
  A.$("#refresh-dashboard").addEventListener("click", () => A.run(load));
  await load();
});
