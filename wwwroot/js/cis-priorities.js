"use strict";
RiskApp.run(async () => {
    const rows = await RiskApp.api("cis-priorities");
    RiskApp.$("#priority-list").innerHTML = RiskApp.table(["CIS Control", "Navn", "Komponenter"], rows.map(r => ["CIS " + r.control.number, RiskApp.esc(r.control.name), r.findings]));
});
