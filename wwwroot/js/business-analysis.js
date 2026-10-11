"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  const list = A.$("#process-list");
  let processes = [], assets = [];
  // Serialize writes so rapidly edited cells cannot overwrite one another.
  let pending = Promise.resolve();
  function write(action) {
    pending = pending.then(action).catch((error) => A.feedback(error.message, true));
  }
  function fieldValue(input) {
    return input.type === "number" ? Number(input.value) : input.dataset.field === "description" ? input.value : input.value.trim();
  }
  function markUnsaved(input) {
    const dirty = input.dataset.field
      ? String(fieldValue(input)) !== input.dataset.saved
      : !!input.value.trim();
    input.classList.toggle("unsaved", dirty);
    input.setAttribute("aria-description", dirty ? "Ikke gemt endnu" : "");
  }
  function fitDescription(input) {
    input.style.height = "auto";
    input.style.height = `${input.scrollHeight + 2}px`;
  }
  const key = (name) => name.trim().toLocaleLowerCase("da-DK");
  function suggestions() {
    const names = new Map(assets.map((asset) => [key(asset.name), asset.name]));
    A.$("#known-assets").innerHTML = [...names.values()]
      .sort((a, b) => a.localeCompare(b, "da-DK"))
      .map((name) => `<option value="${A.esc(name)}"></option>`).join("");
  }
  function tags(row, id) {
    row.querySelector(".asset-tags").innerHTML = assets
      .filter((asset) => asset.businessProcessIds.includes(id))
      .map((asset) => `<span class="asset-tag">${A.esc(asset.name)}<button type="button" class="remove-tag" data-asset="${asset.id}" aria-label="Fjern ${A.esc(asset.name)}" title="Fjern asset">×</button></span>`)
      .join("");
    suggestions();
  }
  function rowMarkup(p) {
    const rating = (field, label) => `<input data-field="${field}" type="number" min="1" max="3" step="1" required value="${p[field]}" aria-label="${label}">`;
    return `<tr data-process="${p.id}">
      <td><input data-field="name" required maxlength="160" value="${A.esc(p.name)}" aria-label="Procesnavn"><textarea data-field="description" rows="1" placeholder="Beskrivelse" aria-label="Procesbeskrivelse">${A.esc(p.description)}</textarea></td>
      <td>${rating("confidentiality", "Fortrolighed")}</td>
      <td>${rating("integrity", "Integritet")}</td>
      <td>${rating("availability", "Tilgængelighed")}</td>
      <td class="consequence">${A.number(p.consequence)}</td>
      <td><div class="asset-tags"></div><input class="add-asset" list="known-assets" placeholder="add asset" aria-label="Tilføj asset" autocomplete="off"></td>
      <td><button type="button" class="danger delete-process" data-delete-process="${p.id}" aria-label="Slet proces">×</button></td>
    </tr>`;
  }
  function appendRow(p) {
    list.querySelector(".add-process-row").insertAdjacentHTML("beforebegin", rowMarkup(p));
    const row = list.querySelector(`[data-process="${p.id}"]`);
    row.querySelectorAll("[data-field]").forEach((input) => {
      input.dataset.saved = String(fieldValue(input));
    });
    fitDescription(row.querySelector("textarea"));
    tags(row, p.id);
    return row;
  }
  [processes, assets] = await Promise.all([A.api("business-processes"), A.api("business-assets")]);
  list.innerHTML = `<div class="table-scroll"><table><thead><tr><th scope="col">Proces</th><th scope="col">C</th><th scope="col">I</th><th scope="col">A</th><th scope="col" tabindex="0" title="Konsekvens: K = 2 × (C + I + A) / 3 − 1, afrundet til nærmeste 0,5. C = fortrolighed, I = integritet, A = tilgængelighed (1–3). Eksempel: C=3, I=3, A=2 giver K=4,5." aria-label="Konsekvens">K</th><th scope="col">Assets</th><th scope="col" aria-label="Handlinger"></th></tr></thead><tbody><tr class="add-process-row"><td colspan="7"><input class="add-process" placeholder="add proces" maxlength="160" aria-label="Tilføj proces" autocomplete="off"></td></tr></tbody></table></div>`;
  processes.forEach(appendRow);
  suggestions();
  const resizeObserver = new ResizeObserver(() => {
    list.querySelectorAll("textarea").forEach(fitDescription);
  });
  resizeObserver.observe(list);

  list.addEventListener("change", (event) => {
    const input = event.target;
    if (!input.dataset.field) return;
    if (!input.reportValidity()) return;
    const row = input.closest("[data-process]");
    const id = Number(row.dataset.process);
    const field = input.dataset.field;
    const value = field === "name" ? input.value.trim() : field === "description" ? input.value : Number(input.value);
    if (field === "name" && !value) {
      input.setCustomValidity("Navn skal udfyldes.");
      input.reportValidity();
      return;
    }
    write(async () => {
      const process = processes.find((p) => p.id === id);
      if (!process) return;
      if (process[field] === value) {
        input.dataset.saved = String(value);
        markUnsaved(input);
        return;
      }
      const saved = await A.api(`business-processes/${id}`, "PUT", { ...process, [field]: value });
      Object.assign(process, saved);
      row.querySelector(".consequence").textContent = A.number(saved.consequence);
      input.dataset.saved = String(saved[field]);
      markUnsaved(input);
    });
  });
  list.addEventListener("input", (event) => {
    const input = event.target;
    input.setCustomValidity?.("");
    if (input.matches("input, textarea")) markUnsaved(input);
    if (input.tagName === "TEXTAREA") fitDescription(input);
  });
  function add(input) {
    const name = input.value.trim();
    if (!name || input.disabled) return;
    input.disabled = true;
    const isProcess = input.classList.contains("add-process");
    const row = input.closest("[data-process]");
    write(async () => {
      try {
        if (isProcess) {
          const process = await A.api("business-processes", "POST", { name, description: "", confidentiality: 1, integrity: 1, availability: 1 });
          processes.push(process);
          appendRow(process);
        } else {
          const id = Number(row.dataset.process);
          const known = assets.find((asset) => key(asset.name) === key(name));
          if (!assets.some((asset) => asset.businessProcessIds.includes(id) && key(asset.name) === key(name))) {
            const asset = await A.api("business-assets", "POST", { businessProcessId: id, name: known?.name || name, description: known?.description || "" });
            assets = assets.filter(existing => existing.id !== asset.id);
            assets.push(asset);
            tags(row, id);
          }
        }
        input.value = "";
        markUnsaved(input);
      } finally {
        input.disabled = false;
        input.focus();
      }
    });
  }
  list.addEventListener("keydown", (event) => {
    if (event.isComposing) return;
    const input = event.target;
    if (event.key === "Enter" && input.tagName === "INPUT") {
      event.preventDefault();
      if (input.matches(".add-process, .add-asset")) add(input);
      else if (input.dataset.field) input.blur();
    }
  });
  list.addEventListener("focusout", (event) => {
    if (event.target.matches(".add-process, .add-asset")) add(event.target);
  });
  list.addEventListener("click", (event) => {
    const button = event.target.closest("button");
    if (!button || button.disabled) return;
    const isProcess = !!button.dataset.deleteProcess;
    if (isProcess && !confirm("Slet processen? Dens asset-tilknytninger fjernes; fælles assets bevares.")) return;
    button.disabled = true;
    write(async () => {
      try {
        const id = Number(isProcess ? button.dataset.deleteProcess : button.dataset.asset);
        const row = button.closest("[data-process]");
        const processId = Number(row.dataset.process);
        await A.api(isProcess ? `business-processes/${id}` : `business-assets/${id}/processes/${processId}`, "DELETE");
        if (isProcess) {
          processes = processes.filter((p) => p.id !== id);
          assets.forEach(asset => { asset.businessProcessIds = asset.businessProcessIds.filter(processId => processId !== id); });
          row.remove();
          suggestions();
        } else {
          const asset = assets.find(asset => asset.id === id);
          asset.businessProcessIds = asset.businessProcessIds.filter(id => id !== processId);
          tags(row, processId);
        }

      } finally {
        button.disabled = false;
      }
    });
  });
});
