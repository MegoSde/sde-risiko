"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  let processes = [],
    assets = [];
  async function load() {
    [processes, assets] = await Promise.all([
      A.api("business-processes"),
      A.api("business-assets"),
    ]);
    A.$("#asset-form select").innerHTML = A.option(processes);
    A.$("#process-list").innerHTML = A.table(
      ["Proces", "C", "I", "A", "Konsekvens", "Assets", "Handlinger"],
      processes.map((p) => [
        A.esc(p.name) + "<p>" + A.esc(p.description) + "</p>",
        p.confidentiality,
        p.integrity,
        p.availability,
        A.number(p.consequence),
        assets
          .filter((a) => a.businessProcessId === p.id)
          .map(
            (a) =>
              "<p><strong>" +
              A.esc(a.name) +
              "</strong> " +
              A.esc(a.description) +
              " " +
              A.actions("business-assets", a.id) +
              "</p>",
          )
          .join("") || "Ingen assets",
        A.actions("business-processes", p.id),
      ]),
    );
  }
  const processForm = A.bindForm("#process-form", "business-processes", load),
    assetForm = A.bindForm("#asset-form", "business-assets", load);
  function preview() {
    const f = processForm.elements;
    A.$("#consequence").value = A.number(
      Math.round(
        ((2 *
          (Number(f.confidentiality.value) +
            Number(f.integrity.value) +
            Number(f.availability.value))) /
          3 -
          1) *
          2,
      ) / 2,
    );
  }
  processForm.addEventListener("input", preview);
  processForm.addEventListener("reset", () => setTimeout(preview, 0));
  A.bindActions(
    "#process-list",
    (type, id) => {
      A.fill(
        type === "business-processes" ? processForm : assetForm,
        (type === "business-processes" ? processes : assets).find(
          (x) => x.id === id,
        ),
      );
      preview();
    },
    load,
  );
  await load();
});
