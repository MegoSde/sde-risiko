"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  const [threats, systems, controls, selected, recommended] =
    await Promise.all(
      [
        "attack-components",
        "system-assets",
        "cis-controls",
        "component-controls",
        "technique-controls",
      ].map((x) => A.api(x)),
    );
  const techniqueIds = [...new Set([...threats.map(t => t.attackTechniqueId), ...recommended.map(t => t.attackTechniqueId)])];
  const techniques = await Promise.all(techniqueIds.map(id => A.api(`attack-techniques/${id}`)));
  A.$("#control-list").innerHTML =
    threats
      .map(
        (t) =>
          '<section><details class="control-attack" name="attack-controls" data-attack="' + t.id + '"><summary><span class="control-attack-title">' +
          A.esc(systems.find((s) => s.id === t.systemAssetId).name) +
          " · " +
          A.esc(
            techniques.find((a) => a.id === t.attackTechniqueId).techniqueId + " – " + techniques.find((a) => a.id === t.attackTechniqueId).name,
          ) +
          '</span><span class="control-selection" data-selection="' + t.id + '"></span></summary><p>' +
          A.esc(t.comment) +
          "</p><fieldset><legend>Relevante Controls</legend>" +
          controls
            .map((c) => {
              const row = selected.find(
                (x) => x.attackComponentId === t.id && x.cisControlId === c.id,
              );
              const isDefault = recommended.some(
                (x) =>
                  x.attackTechniqueId === t.attackTechniqueId &&
                  x.cisControlId === c.id,
              );
              return (
                '<div class="control-row"><label class="check"><input type="checkbox" data-threat="' +
                t.id +
                '" data-control="' +
                c.id +
                '" ' +
                (row?.isRecommended ? "checked" : "") +
                ">CIS " +
                c.number +
                " – " +
                A.esc(c.name) +
                (isDefault ? " (katalogforslag)" : "") +
                "</label><p>" +
                A.esc(c.description) +
                '</p><label>Kommentar<input data-comment="' +
                t.id +
                "-" +
                c.id +
                '" value="' +
                A.esc(row?.comment || "") +
                '"></label><button type="button" data-save="' +
                t.id +
                '" data-control="' +
                c.id +
                '">Gem kommentar</button></div>'
              );
            })
            .join("") +
          "</fieldset></details></section>",
      )
      .join("") || "<p>Opret først Attack Components under Threat Analysis.</p>";
  function updateSummary(id) {
    const chosen = controls.filter(c => selected.some(row => row.attackComponentId === Number(id) && row.cisControlId === c.id && row.isRecommended)).sort((a, b) => a.number - b.number);
    const summary = A.$(`[data-selection="${id}"]`);
    summary.textContent = chosen.length ? `Valgte CIS (${chosen.length}): ${chosen.map(c => "CIS " + c.number).join(", ")}` : "Valgte CIS: Ingen";
    summary.title = chosen.map(c => `CIS ${c.number} – ${c.name}`).join("\n");
  }
  threats.forEach(t => updateSummary(t.id));
  // The shared details name provides native exclusivity; this also supports older browsers.
  A.$("#control-list").addEventListener("toggle", event => {
    const opened = event.target;
    if (!opened.matches(".control-attack") || !opened.open) return;
    document.querySelectorAll("#control-list .control-attack[open]").forEach(other => {
      if (other !== opened) other.open = false;
    });
  }, true);
  A.$("#recommendations").innerHTML = A.table(
    ["Teknik", "Anbefalede CIS Controls"],
    techniques.map((t) => [
      A.esc(t.techniqueId + " – " + t.name),
      controls
        .filter((c) =>
          recommended.some(
            (m) => m.attackTechniqueId === t.id && m.cisControlId === c.id,
          ),
        )
        .map((c) => "CIS " + c.number + " – " + A.esc(c.name))
        .join("<br>"),
    ]),
  );
  async function save(threat, control) {
    const input = A.$(
      '[data-threat="' + threat + '"][data-control="' + control + '"]',
    );
    const comment = A.$(
      '[data-comment="' + threat + "-" + control + '"]',
    ).value;
    const saved = await A.api("component-controls/" + threat + "/" + control, "PUT", {
      isRecommended: input.checked,
      comment,
    });
    const existing = selected.findIndex(row => row.attackComponentId === saved.attackComponentId && row.cisControlId === saved.cisControlId);
    if (existing < 0) selected.push(saved);
    else selected[existing] = saved;
    updateSummary(threat);
    A.feedback("Control-vurdering gemt.");
  }
  A.$("#control-list").addEventListener("change", (e) => {
    const input = e.target;
    if (!input.matches("[data-threat]")) return;
    A.run(async () => {
      input.disabled = true;
      try {
        await save(input.dataset.threat, input.dataset.control);
      } catch (e) {
        input.checked = !input.checked;
        throw e;
      } finally {
        input.disabled = false;
      }
    });
  });
  A.$("#control-list").addEventListener("click", (e) => {
    const button = e.target.closest("[data-save]");
    if (!button) return;
    A.run(async () => {
      button.disabled = true;
      try {
        await save(button.dataset.save, button.dataset.control);
      } finally {
        button.disabled = false;
      }
    });
  });
});
