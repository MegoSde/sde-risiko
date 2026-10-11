"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  const [risks, levels] = await Promise.all([
    A.api("risks"),
    A.api("risk-levels"),
  ]);
  let html =
    '<table class="matrix"><caption>Risici fordelt efter Blast Radius (rækker) og Likelihood (kolonner)</caption><thead><tr><th scope="col">Blast Radius ↓ / Likelihood →</th>' +
    [1, 2, 3, 4, 5].map((x) => '<th scope="col">' + x + "</th>").join("") +
    "</tr></thead><tbody>";
  for (let y = 5; y >= 1; y--) {
    html += '<tr><th scope="row">' + y + "</th>";
    for (let x = 1; x <= 5; x++) {
      const count = risks.filter(
        (r) => Math.ceil(r.blastRadius) === y && r.likelihood === x,
      ).length;
      const score = y * x,
        level =
          score <= levels.lowMax
            ? "low"
            : score < levels.highMin
              ? "medium"
              : score < levels.criticalMin
                ? "high"
                : "critical";
      html +=
        '<td class="' +
        level +
        '"><button type="button" data-x="' +
        x +
        '" data-y="' +
        y +
        '" aria-label="Blast Radius ' +
        y +
        ", likelihood " +
        x +
        ": " +
        count +
        ' risici">' +
        count +
        "</button></td>";
    }
    html += "</tr>";
  }
  A.$("#matrix").innerHTML = html + "</tbody></table>";
  A.$("#risk-list").innerHTML = A.risksTable(risks);
  A.$("#matrix").addEventListener("click", (e) => {
    const button = e.target.closest("[data-x]");
    if (!button) return;
    const x = Number(button.dataset.x),
      y = Number(button.dataset.y);
    A.$("#filter-label").textContent =
      "Blast Radius " +
      (y === 1 ? "1" : "> " + (y - 1) + " og ≤ " + y) +
      " · Likelihood " +
      x;
    A.$("#risk-list").innerHTML = A.risksTable(
      risks.filter((r) => Math.ceil(r.blastRadius) === y && r.likelihood === x),
    );
    for (const b of document.querySelectorAll("[data-x]"))
      b.setAttribute("aria-pressed", String(b === button));
  });
  A.$("#clear-filter").addEventListener("click", () => {
    A.$("#filter-label").textContent = "Alle risici";
    A.$("#risk-list").innerHTML = A.risksTable(risks);
    for (const b of document.querySelectorAll("[data-x]"))
      b.setAttribute("aria-pressed", "false");
  });
});
