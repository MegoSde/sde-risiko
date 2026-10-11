"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  const [processes, systems, risks, priorities] = await Promise.all(
    ["business-processes", "system-assets", "risks", "cis-priorities"].map(
      (x) => A.api(x),
    ),
  );
  const techniques = [
    ...new Map(risks.map((r) => [r.technique.id, r.technique])).values(),
  ];
  A.$("#report").innerHTML =
    "<p>Udarbejdet " +
    A.esc(new Date().toLocaleDateString("da-DK")) +
    "</p><h2>Executive Summary</h2><p>Analysen omfatter " +
    processes.length +
    " processer, " +
    systems.length +
    " systemer og " +
    risks.length +
    " identificerede risici. Højeste score er " +
    A.number(Math.max(0, ...risks.map((r) => r.riskScore))) +
    ". " +
    risks.filter((r) => r.riskLevel === "Critical").length +
    " risici er kritiske.</p><p>Risikoscorer er faglige undervisningsvurderinger. Controls er prioriteret efter summerede scorer og er ikke et estimat af faktisk risikoreduktion.</p><h2>Forretningsprocesser</h2>" +
    A.table(
      ["Proces", "Beskrivelse", "C", "I", "A", "Konsekvens"],
      processes.map((p) => [
        A.esc(p.name),
        A.esc(p.description),
        p.confidentiality,
        p.integrity,
        p.availability,
        A.number(p.consequence),
      ]),
    ) +
    "<h2>Systemassets</h2>" +
    A.table(
      ["System", "Type", "Zone", "Forretningsassets", "Direkte konsekvens"],
      systems.map((s) => [
        A.esc(s.name),
        A.esc(s.assetType),
        A.esc(s.zone),
        A.esc(s.businessAssets.map((a) => a.name).join(", ")),
        A.number(s.directConsequence),
      ]),
    ) +
    "<h2>Top risks</h2>" +
    A.risksTable(risks.slice(0, 10)) +
    "<h2>Systemer med højeste Blast Radius</h2>" +
    A.table(
      ["System", "Blast Radius", "Begrundelse"],
      [...systems]
        .sort((a, b) => b.blastRadius - a.blastRadius)
        .map((s) => [
          A.esc(s.name),
          A.number(s.blastRadius),
          A.esc(s.blastRadiusReason),
        ]),
    ) +
    "<h2>MITRE ATT&amp;CK techniques</h2>" +
    A.table(
      ["Teknik", "Navn", "Tactic", "Beskrivelse"],
      techniques.map((t) => [
        A.esc(t.techniqueId),
        A.esc(t.name),
        A.esc(t.tactic),
        A.esc(t.description),
      ]),
    ) +
    "<h2>Prioriterede CIS Controls</h2>" +
    A.priorityTable(priorities);
  A.$("#print-report").addEventListener("click", () => {
    document
      .querySelectorAll("#report details")
      .forEach((d) => (d.open = true));
    window.print();
  });
});
