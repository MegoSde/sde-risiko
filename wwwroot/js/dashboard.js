"use strict";
RiskApp.run(async () => {
  const A = RiskApp,
    d = await A.api("dashboard");
  A.$("#stats").innerHTML = [
    ["Forretningsprocesser", d.businessProcesses],
    ["Forretningsassets", d.businessAssets],
    ["Systemassets", d.systemAssets],
    ["ATT&CK-fund", d.attackFindings],
    ["Identificerede risici", d.risks],
    ["Højeste risikoscore", d.highestRiskScore],
  ]
    .map(
      ([label, value]) =>
        '<article><strong class="stat">' +
        A.number(value) +
        "</strong><span>" +
        label +
        "</span></article>",
    )
    .join("");
  const top = d.priorities
    .filter((p) => p.findings > 0)
    .sort((a, b) => b.findings - a.findings)
    .slice(0, 5);
  A.$("#common-controls").innerHTML = A.table(
    ["CIS Control", "Navn", "Antal fund"],
    top.map((p) => [
      "CIS " + p.control.number,
      A.esc(p.control.name),
      p.findings,
    ]),
  );
});
