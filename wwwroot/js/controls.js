"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  const [threats, systems, techniques, controls, selected, recommended] =
    await Promise.all(
      [
        "system-threats",
        "system-assets",
        "attack-techniques",
        "cis-controls",
        "threat-controls",
        "technique-controls",
      ].map((x) => A.api(x)),
    );
  A.$("#control-list").innerHTML =
    threats
      .map(
        (t) =>
          "<section><h2>" +
          A.esc(systems.find((s) => s.id === t.systemAssetId).name) +
          " · " +
          A.esc(
            techniques.find((a) => a.id === t.attackTechniqueId).techniqueId,
          ) +
          "</h2><p>" +
          A.esc(t.scenario) +
          "</p><fieldset><legend>Relevante Controls</legend>" +
          controls
            .map((c) => {
              const row = selected.find(
                (x) => x.systemThreatId === t.id && x.cisControlId === c.id,
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
          "</fieldset></section>",
      )
      .join("") || "<p>Opret først ATT&amp;CK-fund under Threat Analysis.</p>";
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
    await A.api("threat-controls/" + threat + "/" + control, "PUT", {
      isRecommended: input.checked,
      comment,
    });
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
