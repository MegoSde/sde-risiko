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
  function riskScenarioTable(rows, compact = false, selectedId = null) {
    if (!rows.length) return '<p class="empty">Ingen scenarier her. Opret startkomponenter fra Attack Surface på Threat Analysis, eller vælg en anden matrixcelle.</p>';
    const headers = compact ? ["Entry Point", "ATT&CK", "Consequence Index", "Scenario Likelihood", "Risk Score", "Risk Level"] : ["Entry Point", "Initial ATT&CK", "Initial Likelihood", "Paths", "Reachable Business Assets", "Accumulated Consequence", "Consequence Index", "Scenario Likelihood", "Risk Score", "Risk Level"];
    return '<div class="table-scroll"><table class="risk-scenarios"><thead><tr>' + headers.map(h => `<th scope="col">${esc(h)}</th>`).join("") + '</tr></thead><tbody>' + rows.map(r => {
      const first = r.initialAttackComponent;
      const cells = [`<button type="button" data-risk="${r.id}" aria-expanded="${r.id === selectedId}" aria-controls="risk-details">${esc(first.systemAssetName)}</button>`, `<span title="${esc(first.techniqueName)}">${esc(first.techniqueId)}</span>`];
      if (!compact) cells.push(first.likelihood, r.paths.length, r.reachableBusinessAssets.length, number(r.accumulatedConsequence));
      cells.push(r.consequenceIndex, r.scenarioLikelihood, r.riskScore, `<span class="badge ${r.riskLevel.toLowerCase()}">${esc(r.riskLevel)}</span>`);
      return `<tr data-risk="${r.id}">${cells.map(c => `<td>${c}</td>`).join("")}</tr>`;
    }).join("") + '</tbody></table></div>';
  }
  function riskScenarioDetails(r) {
    const first = r.initialAttackComponent;
    const componentLabel = c => `${c.systemAssetName} / ${c.techniqueId} / P${c.likelihood}`;
    const paths = r.paths.map(path => {
      const chain = path.components.map((c, i) => `<span title="${esc(c.techniqueName)}">${esc(componentLabel(c))}</span>` + (i < path.components.length - 1 ? ` <span class="muted">—${esc(path.connections[i].relationType)}→</span> ` : "")).join("");
      const cycle = path.cycleToComponentId ? path.components.find(c => c.id === path.cycleToComponentId) : null;
      return `<li><h3>Path ${path.number}</h3><p class="risk-path">${chain}${cycle ? ` <span class="muted">↻ ${esc(componentLabel(cycle))} (cycle: path stoppet før gentagelse)</span>` : ""}</p><p>PathLikelihood = MIN(${path.components.map(c => c.likelihood).join(", ")}) = <strong>${path.pathLikelihood}</strong></p></li>`;
    }).join("");
    const assets = table(["Business Asset", "Business Processes og konsekvenser", "Asset consequence", "Nået via"], r.reachableBusinessAssets.map(a => [esc(a.name), a.businessProcesses.length ? a.businessProcesses.map(p => `${esc(p.name)} (${number(p.consequence)})`).join("<br>") : "Ingen procestilknytning (0)", `${a.businessProcesses.map(p => number(p.consequence)).join(" + ") || "0"} = <strong>${number(a.consequence)}</strong>`, esc([...new Map(r.paths.flatMap(p => p.components).filter(c => a.reachedViaSystemAssetIds.includes(c.systemAssetId)).map(c => [c.systemAssetId, c.systemAssetName])).values()].join(", "))]));
    return `<h2>${esc(first.systemAssetName)} · ${esc(first.techniqueId)} – ${esc(first.techniqueName)}</h2><p>Initial Likelihood: ${first.likelihood}</p><p>${esc(first.comment)}</p><h3>Attack Paths</h3><p>Kun afsluttede paths medtages: en slutkomponent eller en cycle, som stoppes før en komponent besøges igen. En startkomponent uden forbindelser udgør ét path.</p><ol class="risk-paths">${paths}</ol><h3>Reachable Business Assets</h3><p>Hvert asset tælles én gang, selv hvis flere paths eller systemer når det. Asset consequence er summen fra dets tilknyttede processer.</p>${assets}<dl class="risk-calculations"><dt>Accumulated Consequence</dt><dd>SUM af distinct nåede Business Assets: ${r.reachableBusinessAssets.map(a => number(a.consequence)).join(" + ") || "0"} = <strong>${number(r.accumulatedConsequence)}</strong></dd><dt>Maximum Consequence</dt><dd>SUM af konsekvens for alle distinct Business Assets i modellen = <strong>${number(r.maximumConsequence)}</strong></dd><dt>Impact Ratio</dt><dd>${r.maximumConsequence === 0 ? "Samlet konsekvens er 0. Division springes over; Impact Ratio sættes til <strong>0 %</strong>." : `${number(r.accumulatedConsequence)} / ${number(r.maximumConsequence)} = <strong>${number(r.impactRatio * 100)} %</strong>`}</dd><dt>Consequence Index</dt><dd>0–20 % → 1; &gt;20–40 % → 2; &gt;40–60 % → 3; &gt;60–80 % → 4; &gt;80–100 % → 5. Resultat: <strong>${r.consequenceIndex}</strong></dd><dt>Scenario Likelihood</dt><dd>MAX(${r.paths.map(p => p.pathLikelihood).join(", ")}) = <strong>${r.scenarioLikelihood}</strong></dd><dt>Risk Score</dt><dd>Consequence Index × Scenario Likelihood = ${r.consequenceIndex} × ${r.scenarioLikelihood} = <strong>${r.riskScore}</strong> (${esc(r.riskLevel)})</dd></dl>`;
  }
  function bindRiskScenarios(target, details, find, selected) {
    target.addEventListener("click", event => {
      const clicked = event.target.closest("[data-risk]"); if (!clicked) return;
      const id = Number(clicked.dataset.risk), row = find(id); if (!row) return;
      selected(id);
      target.querySelectorAll("button[data-risk]").forEach(button => button.setAttribute("aria-expanded", String(Number(button.dataset.risk) === id)));
      details.innerHTML = riskScenarioDetails(row); details.hidden = false;
      details.focus({ preventScroll: true });
      details.scrollIntoView({ behavior: "smooth", block: "start" });
    });
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
    systemForm,
    riskScenarioTable,
    riskScenarioDetails,
    bindRiskScenarios,
  };
})();
