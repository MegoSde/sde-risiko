"use strict";
RiskApp.run(async () => {
  RiskApp.$("#priority-list").innerHTML = RiskApp.priorityTable(
    await RiskApp.api("cis-priorities"),
  );
});
