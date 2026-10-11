"use strict";
window.RiskApp = (() => {
  const $ = (s) => document.querySelector(s);
  const esc = (value) =>
    String(value ?? "").replace(
      /[&<>"']/g,
      (c) =>
        ({
          "&": "&amp;",
          "<": "&lt;",
          ">": "&gt;",
          '"': "&quot;",
          "'": "&#39;",
        })[c],
    );
  const number = (n) =>
    Number(n).toLocaleString("da-DK", { maximumFractionDigits: 2 });
  function feedback(message, error = false) {
    const el = $("#feedback");
    el.textContent = message;
    el.className = error ? "error" : "success";
  }
  async function api(path, method = "GET", body) {
    const response = await fetch("/api/" + path, {
      method,
      headers: body === undefined ? {} : { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) {
      let detail;
      try {
        detail = await response.json();
      } catch {
        detail = {};
      }
      throw new Error(
        detail.error ||
          (detail.errors
            ? Object.values(detail.errors).flat().join(" ")
            : null) ||
          detail.detail ||
          "Handlingen fejlede (" + response.status + ").",
      );
    }
    return response.status === 204 ? null : response.json();
  }
  const run = (fn) =>
    Promise.resolve()
      .then(fn)
      .catch((e) => feedback(e.message, true));
  const table = (headers, rows) =>
    rows.length
      ? '<div class="table-scroll"><table><thead><tr>' +
        headers.map((x) => '<th scope="col">' + esc(x) + "</th>").join("") +
        "</tr></thead><tbody>" +
        rows
          .map(
            (row) =>
              "<tr>" +
              row.map((cell) => "<td>" + cell + "</td>").join("") +
              "</tr>",
          )
          .join("") +
        "</tbody></table></div>"
      : '<p class="empty">Ingen data endnu. Start med at oprette grundlaget i de foregående trin.</p>';
  const option = (rows, label = (x) => x.name) =>
    rows
      .map((x) => '<option value="' + x.id + '">' + esc(label(x)) + "</option>")
      .join("");
  const actions = (resource, id) =>
    '<button type="button" data-edit="' +
    resource +
    '" data-id="' +
    id +
    '">Rediger</button> <button type="button" class="danger" data-delete="' +
    resource +
    '" data-id="' +
    id +
    '">Slet</button>';
  const fill = (form, row) => {
    for (const [name, value] of Object.entries(row)) {
      const input = form.elements.namedItem(name);
      if (input) input.value = value;
    }
    form.scrollIntoView({ behavior: "smooth", block: "center" });
  };
  const numeric = new Set([
    "id",
    "businessProcessId",
    "systemAssetId",
    "attackTechniqueId",
    "confidentiality",
    "integrity",
    "availability",
    "blastRadius",
    "likelihood",
  ]);
  function bindForm(selector, resource, onSaved) {
    const form = $(selector);
    form.addEventListener("reset", () => {
      setTimeout(() => {
        form.elements.id.value = "";
      }, 0);
    });
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      run(async () => {
        const body = Object.fromEntries(new FormData(form));
        for (const key of numeric)
          if (key in body) body[key] = Number(body[key]);
        const id = body.id;
        delete body.id;
        const button = form.querySelector("button");
        button.disabled = true;
        try {
          await api(resource + (id ? "/" + id : ""), id ? "PUT" : "POST", body);
          form.reset();
          form.elements.id.value = "";
          await onSaved();
          feedback("Gemt.");
        } finally {
          button.disabled = false;
        }
      });
    });
    return form;
  }
  function bindActions(container, edit, onDeleted) {
    $(container).addEventListener("click", (e) => {
      const button = e.target.closest("button");
      if (!button) return;
      if (button.dataset.edit)
        edit(button.dataset.edit, Number(button.dataset.id));
      if (button.dataset.delete && confirm("Slet posten og tilknyttede data?"))
        run(async () => {
          button.disabled = true;
          try {
            await api(
              button.dataset.delete + "/" + button.dataset.id,
              "DELETE",
            );
            await onDeleted();
            feedback("Slettet.");
          } catch (e) {
            button.disabled = false;
            throw e;
          }
        });
    });
  }
  function risksTable(rows) {
    return table(
      [
        "Systemasset",
        "ATT&CK",
        "Blast Radius",
        "Likelihood",
        "Score",
        "Niveau",
        "Controls",
        "Detaljer",
      ],
      rows.map((r) => [
        esc(r.systemAsset.name),
        esc(r.technique.techniqueId + " – " + r.technique.name),
        number(r.blastRadius),
        number(r.likelihood),
        number(r.riskScore),
        '<span class="badge ' +
          r.riskLevel.toLowerCase() +
          '">' +
          r.riskLevel +
          "</span>",
        r.cisControls.map((c) => "CIS " + c.number).join(", "),
        "<details><summary>Vis grundlag</summary><dl><dt>Processer</dt><dd>" +
          esc(
            r.businessProcesses
              .map((p) => p.name + " (" + number(p.consequence) + ")")
              .join(", ") || "Ingen mapping",
          ) +
          "</dd><dt>Forretningsassets</dt><dd>" +
          esc(
            r.businessAssets.map((a) => a.name).join(", ") || "Ingen mapping",
          ) +
          "</dd><dt>Scenarie</dt><dd>" +
          esc(r.scenario) +
          "</dd><dt>Blast Radius-begrundelse</dt><dd>" +
          esc(r.systemAsset.blastRadiusReason || "Ikke angivet") +
          "</dd><dt>Tactic</dt><dd>" +
          esc(r.technique.tactic) +
          "</dd><dt>CIS Controls</dt><dd>" +
          esc(
            r.cisControls.map((c) => c.number + " – " + c.name).join(", ") ||
              "Ingen valgt",
          ) +
          '</dd></dl><a href="' +
          esc(r.technique.mitreUrl) +
          '" target="_blank" rel="noopener">Læs teknikken hos MITRE</a></details>',
      ]),
    );
  }
  function priorityTable(rows) {
    return table(
      ["CIS Control", "Navn", "Fund", "Vægtet score", "Gennemsnit", "Andel"],
      rows.map((r) => [
        "CIS " + r.control.number,
        esc(r.control.name),
        number(r.findings),
        number(r.weightedRiskScore) +
          '<meter min="0" max="' +
          Math.max(1, ...rows.map((x) => x.weightedRiskScore)) +
          '" value="' +
          r.weightedRiskScore +
          '">' +
          number(r.weightedRiskScore) +
          "</meter>",
        number(r.averageRiskScore),
        number(r.share * 100) + " %",
      ]),
    );
  }
  async function systemForm(onSaved) {
    return bindForm("#system-form", "system-assets", onSaved);
  }
  return {
    $,
    esc,
    number,
    feedback,
    api,
    run,
    table,
    option,
    actions,
    fill,
    bindForm,
    bindActions,
    risksTable,
    priorityTable,
    systemForm,
  };
})();
