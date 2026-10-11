"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  let threats = [],
    systems = [],
    techniques = [];
  function info() {
    const t = techniques.find(
      (t) => t.id === Number(A.$('[name="attackTechniqueId"]').value),
    );
    A.$("#technique-info").innerHTML = t
      ? "<h3>" +
        A.esc(t.name) +
        "</h3><p><strong>Tactic:</strong> " +
        A.esc(t.tactic) +
        "</p><p>" +
        A.esc(t.description) +
        '</p><a href="' +
        A.esc(t.mitreUrl) +
        '" target="_blank" rel="noopener">MITRE ATT&amp;CK</a>'
      : "";
  }
  async function load() {
    [threats, systems, techniques] = await Promise.all(
      ["system-threats", "system-assets", "attack-techniques"].map((x) =>
        A.api(x),
      ),
    );
    A.$('[name="systemAssetId"]').innerHTML = A.option(systems);
    A.$('[name="attackTechniqueId"]').innerHTML = A.option(
      techniques,
      (t) => t.techniqueId + " – " + t.name,
    );
    info();
    A.$("#threat-list").innerHTML = A.table(
      ["System", "Teknik", "Scenarie", "Sandsynlighed", "Handlinger"],
      threats.map((t) => [
        A.esc(systems.find((s) => s.id === t.systemAssetId).name),
        A.esc(techniques.find((a) => a.id === t.attackTechniqueId).techniqueId),
        A.esc(t.scenario),
        t.likelihood,
        A.actions("system-threats", t.id),
      ]),
    );
  }
  const form = A.bindForm("#threat-form", "system-threats", load);
  A.$('[name="attackTechniqueId"]').addEventListener("change", info);
  form.addEventListener("reset", () => setTimeout(info, 0));
  A.bindActions(
    "#threat-list",
    (type, id) => {
      A.fill(
        form,
        threats.find((t) => t.id === id),
      );
      info();
    },
    load,
  );
  await load();
});
