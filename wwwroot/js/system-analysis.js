"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  let systems = [],
    access = [];
  async function load() {
    [systems, access] = await Promise.all([
      A.api("system-assets"),
      A.api("system-access"),
    ]);
    A.$("#system-list").innerHTML =
      systems
        .map(
          (s) =>
            "<section><h2>" +
            A.esc(s.name) +
            "</h2><p>" +
            A.esc(s.assetType) +
            " · Zone: " +
            A.esc(s.zone || "Ikke angivet") +
            "</p><p>" +
            A.esc(s.description) +
            "</p><p>Forretningsassets: " +
            A.esc(
              s.businessAssets.map((a) => a.name).join(", ") || "Ingen mapping",
            ) +
            "</p><p>Direkte konsekvens: <strong>" +
            A.number(s.directConsequence) +
            "</strong> · Blast Radius: <strong>" +
            A.number(s.blastRadius) +
            "</strong></p><p>Begrundelse: " +
            A.esc(s.blastRadiusReason || "Ikke angivet") +
            "</p>" +
            A.actions("system-assets", s.id) +
            "<fieldset><legend>Kan give adgang videre til</legend>" +
            systems
              .filter((t) => t.id !== s.id)
              .map((t) => {
                const link = access.find(
                  (a) =>
                    a.sourceSystemAssetId === s.id &&
                    a.targetSystemAssetId === t.id,
                );
                return (
                  '<div class="access-row"><label class="check"><input type="checkbox" data-source="' +
                  s.id +
                  '" data-target="' +
                  t.id +
                  '" ' +
                  (link ? "checked" : "") +
                  ">" +
                  A.esc(t.name) +
                  '</label><label>Adgangsbeskrivelse<input data-description="' +
                  s.id +
                  "-" +
                  t.id +
                  '" value="' +
                  A.esc(link?.description || "") +
                  '"></label><button type="button" data-save-access="' +
                  s.id +
                  '" data-target="' +
                  t.id +
                  '">Gem beskrivelse</button></div>'
                );
              })
              .join("") +
            "</fieldset></section>",
        )
        .join("") || "<p>Opret det første system nedenfor.</p>";
  }
  const form = await A.systemForm(load);
  A.bindActions(
    "#system-list",
    (type, id) =>
      A.fill(
        form,
        systems.find((x) => x.id === id),
      ),
    load,
  );
  async function save(source, target, selected) {
    const row = access.find(
      (x) =>
        x.sourceSystemAssetId === source && x.targetSystemAssetId === target,
    );
    if (selected) {
      const description = A.$(
        '[data-description="' + source + "-" + target + '"]',
      ).value;
      const saved = await A.api("system-access", "POST", {
        sourceSystemAssetId: source,
        targetSystemAssetId: target,
        description,
      });
      access = access.filter((x) => x.id !== saved.id);
      access.push(saved);
    } else if (row) {
      await A.api("system-access/" + row.id, "DELETE");
      access = access.filter((x) => x.id !== row.id);
    }
    A.feedback("Adgang gemt.");
  }
  A.$("#system-list").addEventListener("change", (e) => {
    const el = e.target;
    if (!el.matches("[data-source]")) return;
    A.run(async () => {
      el.disabled = true;
      try {
        await save(
          Number(el.dataset.source),
          Number(el.dataset.target),
          el.checked,
        );
      } catch (e) {
        el.checked = !el.checked;
        throw e;
      } finally {
        el.disabled = false;
      }
    });
  });
  A.$("#system-list").addEventListener("click", (e) => {
    const el = e.target.closest("[data-save-access]");
    if (!el) return;
    A.run(async () => {
      const source = Number(el.dataset.saveAccess),
        target = Number(el.dataset.target);
      if (
        !A.$('[data-source="' + source + '"][data-target="' + target + '"]')
          .checked
      )
        throw new Error("Vælg først adgangen.");
      el.disabled = true;
      try {
        await save(source, target, true);
      } finally {
        el.disabled = false;
      }
    });
  });
  await load();
});
