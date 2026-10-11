"use strict";
RiskApp.run(async () => {
  const A = RiskApp;
  const root = A.$("#asset-model");
  const assetTypes = ["Host", "Data", "Application", "Service", "Network", "Endpoint", "Identity", "CloudService", "Person", "External", "Other"];
  const businessTypes = ["StoredIn", "ProcessedBy", "UsedBy", "ProtectedBy", "DependsOn", "RepresentedBy", "Other"];
  const systemTypes = ["HostedOn", "LocatedIn", "ConnectsTo", "AuthenticatesVia", "AccessedVia", "AdministeredFrom", "Uses", "DependsOn", "MemberOf", "ProtectedBy", "RoutesThrough", "Other"];
  let assets = [], processes = [], systems = [], businessLinks = [], systemLinks = [];
  const options = (values) => values.map(value => `<option value="${A.esc(value)}">${A.esc(value)}</option>`).join("");
  const resource = (kind) => kind === "business" ? "business-asset-relations" : "system-asset-relations";
  const links = (kind) => kind === "business" ? businessLinks : systemLinks;
  const sourceName = (kind, id) => (kind === "business" ? assets : systems).find(asset => asset.id === id)?.name || "Ukendt asset";
  function systemFields() {
    return `<label>Navn<input name="name" required></label>
      <label>Type<select name="assetType">${options(assetTypes)}</select></label>
      <label>Beskrivelse<textarea name="description" rows="2"></textarea></label>`;
  }
  const systemForm = A.$("#system-form");
  systemForm.innerHTML = `<input type="hidden" name="id">${systemFields()}<button>Gem system</button><button type="reset">Annuller</button>`;
  function relationList(kind, id) {
    const rows = links(kind).filter(link => (kind === "business" ? link.businessAssetId : link.sourceSystemAssetId) === id);
    return rows.length ? `<ul class="model-relations">${rows.map(link => {
      const target = systems.find(system => system.id === (kind === "business" ? link.systemAssetId : link.targetSystemAssetId));
      return `<li><div class="relation-direction">${A.esc(sourceName(kind, id))} <span class="relation-type">—${A.esc(link.relationType)}→</span> ${A.esc(target?.name || "Ukendt system")}</div>
        ${link.description ? `<p>${A.esc(link.description)}</p>` : ""}
        <button type="button" data-edit-relation="${link.id}" data-kind="${kind}">Rediger relation</button>
        <button type="button" class="danger" data-delete-relation="${link.id}" data-kind="${kind}">Slet relation</button></li>`;
    }).join("")}</ul>` : `<p class="empty">Ingen relationer endnu.</p>`;
  }
  function relationEditor(kind, id) {
    return `<details class="relation-editor"><summary>+ Tilføj relation</summary>
      <form class="relation-form" data-kind="${kind}" data-source="${id}">
        <input type="hidden" name="id">
        <label>System Asset<select name="targetId" required>
          <option value="">Vælg System Asset</option>
          ${A.option(systems.filter(system => kind === "business" || system.id !== id), system => `${system.name} · ${system.assetType}`)}
          <option value="new">+ Opret nyt System Asset</option>
        </select></label>
        <fieldset class="new-system" hidden disabled><legend>Nyt System Asset</legend>${systemFields()}</fieldset>
        <label>Relation<select name="relationType">${options(kind === "business" ? businessTypes : systemTypes)}</select></label>
        <label>Beskrivelse<input name="relationDescription" placeholder="Valgfri"></label>
        <button>Gem relation</button><button type="reset">Annuller</button>
      </form></details>`;
  }
  async function load() {
    const open = new Set([...root.querySelectorAll("details[data-system][open]")].map(detail => detail.dataset.system));
    [assets, processes, systems, businessLinks, systemLinks] = await Promise.all([
      "business-assets", "business-processes", "system-assets", "business-asset-relations", "system-asset-relations"
    ].map(path => A.api(path)));
    A.$("#business-asset-list").innerHTML = assets.map(asset => {
      const linkedProcesses = processes.filter(process => asset.businessProcessIds.includes(process.id));
      return `<article data-business="${asset.id}"><div class="asset-heading"><h3>${A.esc(asset.name)}</h3><button type="button" data-rename-asset="${asset.id}">Omdøb</button><button type="button" class="danger" data-delete-business="${asset.id}">Slet asset</button></div>
        <form class="asset-name-form" data-asset="${asset.id}" hidden>
          <label>Assetnavn<input name="name" value="${A.esc(asset.name)}" required></label>
          <button>Gem navn</button><button type="reset">Annuller</button>
        </form>
        ${linkedProcesses.length ? `<p>Forretningsprocesser:</p><ul class="asset-processes">${linkedProcesses.map(process => `<li>${A.esc(process.name)} · Konsekvens: <strong>${A.number(process.consequence)}</strong></li>`).join("")}</ul>` : `<p class="empty">Ingen forretningsprocesser tilknyttet.</p>`}
        ${asset.description ? `<p>${A.esc(asset.description)}</p>` : ""}
        ${relationList("business", asset.id)}${relationEditor("business", asset.id)}</article>`;
    }).join("") || `<p class="empty">Tilføj Business Assets under <a href="/business-analysis">Forretningsanalyse</a>.</p>`;
    A.$("#model-system-list").innerHTML = systems.map(system => `<article><details data-system="${system.id}" ${open.has(String(system.id)) ? "open" : ""}>
      <summary><strong>${A.esc(system.name)}</strong> · Type: ${A.esc(system.assetType)}</summary>
      ${system.description ? `<p>${A.esc(system.description)}</p>` : ""}
      ${A.actions("system-assets", system.id)}
      ${relationList("system", system.id)}${relationEditor("system", system.id)}</details></article>`).join("") || `<p class="empty">Opret det første System Asset nedenfor eller fra en relation.</p>`;
  }
  root.addEventListener("change", event => {
    if (event.target.name !== "targetId") return;
    const fieldset = event.target.form.querySelector(".new-system");
    fieldset.hidden = fieldset.disabled = event.target.value !== "new";
  });
  root.addEventListener("reset", event => {
    const form = event.target;
    if (form.matches(".asset-name-form")) {
      form.hidden = true;
      form.elements.name.classList.remove("unsaved");
      return;
    }
    form.elements.id.value = "";
    if (form === systemForm) {
      A.$("#system-editor").open = false;
    } else {
      const fieldset = form.querySelector(".new-system");
      fieldset.hidden = fieldset.disabled = true;
      form.closest("details").open = false;
    }
  });
  root.addEventListener("submit", event => {
    event.preventDefault();
    const form = event.target;
    const button = form.querySelector('button:not([type="reset"])');
    if (button.disabled) return;
    button.disabled = true;
    A.run(async () => {
      try {
        const values = Object.fromEntries(new FormData(form));
        if (form.matches(".asset-name-form")) {
          const asset = assets.find(asset => asset.id === Number(form.dataset.asset));
          await A.api(`business-assets/${asset.id}`, "PUT", { name: values.name, description: asset.description });
        } else if (form === systemForm) {
          const previous = systems.find(system => system.id === Number(values.id));
          await A.api("system-assets" + (values.id ? `/${values.id}` : ""), values.id ? "PUT" : "POST", {
            ...(previous || {}), name: values.name, assetType: values.assetType, description: values.description
          });
          form.reset();
        } else {
          let target = Number(values.targetId);
          if (values.targetId === "new") {
            const created = await A.api("system-assets", "POST", {
              name: values.name, assetType: values.assetType, description: values.description
            });
            // Keep the created target if saving the relation fails; retry must not create it twice.
            systems.push(created);
            const select = form.elements.targetId;
            select.add(new Option(`${created.name} · ${created.assetType}`, String(created.id)), select.lastElementChild);
            select.value = String(created.id);
            const fieldset = form.querySelector(".new-system");
            fieldset.hidden = fieldset.disabled = true;
            target = created.id;
          }
          const kind = form.dataset.kind;
          const body = { relationType: values.relationType, description: values.relationDescription };
          if (kind === "business") {
            body.businessAssetId = Number(form.dataset.source);
            body.systemAssetId = target;
          } else {
            body.sourceSystemAssetId = Number(form.dataset.source);
            body.targetSystemAssetId = target;
          }
          await A.api(resource(kind) + (values.id ? `/${values.id}` : ""), values.id ? "PUT" : "POST", body);
        }
        await load();
        A.feedback("");
      } finally {
        button.disabled = false;
      }
    });
  });
  root.addEventListener("input", event => {
    const input = event.target;
    if (!input.form?.matches(".asset-name-form")) return;
    const asset = assets.find(asset => asset.id === Number(input.form.dataset.asset));
    input.classList.toggle("unsaved", input.value.trim() !== asset.name);
  });
  root.addEventListener("click", event => {
    const button = event.target.closest("button");
    if (!button || button.disabled) return;
    if (button.dataset.renameAsset) {
      const form = button.closest("article").querySelector(".asset-name-form");
      form.hidden = false;
      form.elements.name.focus();
      form.elements.name.select();
    }
    if (button.dataset.editRelation) {
      const kind = button.dataset.kind;
      const link = links(kind).find(link => link.id === Number(button.dataset.editRelation));
      const form = button.closest("article").querySelector(".relation-form");
      form.reset();
      form.elements.id.value = link.id;
      form.elements.targetId.value = kind === "business" ? link.systemAssetId : link.targetSystemAssetId;
      form.elements.relationType.value = link.relationType;
      form.elements.relationDescription.value = link.description;
      form.closest("details").open = true;
      form.elements.targetId.focus();
    }
    if (button.dataset.edit) {
      A.$("#system-editor").open = true;
      A.fill(systemForm, systems.find(system => system.id === Number(button.dataset.id)));
    }
    if (button.dataset.deleteRelation || button.dataset.delete || button.dataset.deleteBusiness) {
      if (!confirm(button.dataset.deleteBusiness ? "Slet asset og alle dets procestilknytninger og systemrelationer?" : "Slet posten og tilknyttede data?")) return;
      button.disabled = true;
      A.run(async () => {
        try {
          const path = button.dataset.deleteBusiness
            ? `business-assets/${button.dataset.deleteBusiness}`
            : button.dataset.deleteRelation
            ? `${resource(button.dataset.kind)}/${button.dataset.deleteRelation}`
            : `system-assets/${button.dataset.id}`;
          await A.api(path, "DELETE");
          if (button.dataset.delete && systemForm.elements.id.value === button.dataset.id) systemForm.reset();
          await load();
        } finally {
          button.disabled = false;
        }
      });
    }
  });
  await load();
});
