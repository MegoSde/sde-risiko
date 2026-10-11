"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  let mappings = [];
  async function load() {
    const [assets, processes, systems, links] = await Promise.all(
      [
        "business-assets",
        "business-processes",
        "system-assets",
        "asset-mappings",
      ].map((x) => A.api(x)),
    );
    mappings = links;
    A.$("#mapping-list").innerHTML =
      assets
        .map((a) => {
          const p = processes.find((p) => p.id === a.businessProcessId);
          return (
            "<section><h2>" +
            A.esc(a.name) +
            "</h2><p>Proces: " +
            A.esc(p.name) +
            " · Konsekvens: " +
            A.number(p.consequence) +
            "</p><fieldset><legend>Findes i / afhænger af</legend>" +
            systems
              .map(
                (s) =>
                  '<label class="check"><input type="checkbox" data-asset="' +
                  a.id +
                  '" data-system="' +
                  s.id +
                  '" ' +
                  (links.some(
                    (m) =>
                      m.businessAssetId === a.id && m.systemAssetId === s.id,
                  )
                    ? "checked"
                    : "") +
                  ">" +
                  A.esc(s.name) +
                  " · " +
                  A.esc(s.assetType) +
                  "</label>",
              )
              .join("") +
            (systems.length ? "" : "<p>Opret et systemasset nedenfor.</p>") +
            "</fieldset></section>"
          );
        })
        .join("") ||
      "<p>Opret først et forretningsasset under Forretningsanalyse.</p>";
  }
  A.$("#mapping-list").addEventListener("change", (e) => {
    const input = e.target;
    if (!input.matches("[data-asset]")) return;
    A.run(async () => {
      input.disabled = true;
      try {
        const a = Number(input.dataset.asset),
          s = Number(input.dataset.system);
        if (input.checked) {
          const row = await A.api("asset-mappings", "POST", {
            businessAssetId: a,
            systemAssetId: s,
          });
          mappings.push(row);
        } else {
          const row = mappings.find(
            (m) => m.businessAssetId === a && m.systemAssetId === s,
          );
          await A.api("asset-mappings/" + row.id, "DELETE");
          mappings = mappings.filter((m) => m.id !== row.id);
        }
        A.feedback("Mapping gemt.");
      } catch (e) {
        input.checked = !input.checked;
        throw e;
      } finally {
        input.disabled = false;
      }
    });
  });
  await A.systemForm(load);
  await load();
});
