"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  const [risks, levels] = await Promise.all([
    A.api("risks"),
    A.api("risk-levels"),
  ]);
  A.$("#risk-formula").textContent =
    `Risikoscore = Blast Radius × sandsynlighed. Low ≤ ${A.number(levels.lowMax)}; Medium > ${A.number(levels.lowMax)} og < ${A.number(levels.highMin)}; High ≥ ${A.number(levels.highMin)} og < ${A.number(levels.criticalMin)}; Critical ≥ ${A.number(levels.criticalMin)}.`;
  A.$("#risk-list").innerHTML = A.risksTable(risks);
});
