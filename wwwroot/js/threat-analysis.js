"use strict";
RiskApp.run(async () => {
    const A = RiskApp;
    let components = [], connections = [], assets = [], surface = [], relations = [], selectedId = null;
    const start = A.$("#start-component-form"), pickers = new WeakMap();
    const asset = id => assets.find(a => a.id === id);
    const component = id => components.find(c => c.id === id);
    const title = c => `${asset(c.systemAssetId)?.name || ""} · ${c.technique.techniqueId} – ${c.technique.name}`;
    const areaSize = area => { area.style.height = "auto"; area.style.height = `${area.scrollHeight + 2}px`; };
    function assessment(form, technique) {
        const target = Number(form.elements.systemAssetId?.value || form.dataset.target);
        const existing = components.find(c => c.systemAssetId === target && c.attackTechniqueId === technique?.id);
        for (const field of ["likelihood", "comment"]) {
            form.elements[field].disabled = Boolean(existing);
            if (existing) form.elements[field].value = existing[field];
        }
        form.querySelector("[data-existing]").textContent = existing ? `Eksisterende vurdering genbruges: Likelihood ${existing.likelihood}. Vurderingen kan ændres ved at vælge komponenten.` : "";
        const button = form.querySelector('[type="submit"]');
        button.textContent = existing ? (form === start ? "Vælg eksisterende startkomponent" : "Tilføj forbindelse til eksisterende komponent") : (form === start ? "Opret startkomponent" : "Tilføj næste attack component");
        areaSize(form.elements.comment);
    }
    function picker(form) {
        const root = form.querySelector(".component-picker");
        root.innerHTML = `<label>ATT&amp;CK-teknik (søg ID, navn eller tactic)<input type="search" autocomplete="off" placeholder="Fx T1190, phishing eller Initial Access" required></label><div class="technique-results"></div><p data-selected role="status">Vælg en teknik fra kataloget.</p>`;
        const search = root.querySelector("input"), results = root.querySelector(".technique-results");
        let value = null, matches = [], timer, version = 0;
        function choose(technique) {
            value = technique; search.value = technique ? `${technique.techniqueId} – ${technique.name}` : "";
            search.setCustomValidity(technique ? "" : "Vælg en teknik fra kataloget.");
            root.querySelector("[data-selected]").textContent = technique ? `${technique.techniqueId} – ${technique.name} · ${technique.tactic}` : "Vælg en teknik fra kataloget.";
            results.innerHTML = ""; clearTimeout(timer); version++;
            assessment(form, technique);
        }
        search.addEventListener("input", () => {
            value = null; version++; clearTimeout(timer);
            search.setCustomValidity("Vælg en teknik fra kataloget."); results.innerHTML = "";
            root.querySelector("[data-selected]").textContent = "Vælg en teknik fra kataloget."; assessment(form, null);
            const term = search.value.trim(), current = version;
            timer = setTimeout(async () => {
                try {
                    const rows = await A.api(`attack-techniques?search=${encodeURIComponent(term)}`);
                    if (current !== version) return;
                    matches = rows;
                    results.innerHTML = rows.map(t => `<button type="button" data-technique="${t.id}"><strong>${A.esc(t.techniqueId)} – ${A.esc(t.name)}</strong><span>${A.esc(t.tactic)}</span></button>`).join("") || "Ingen teknikker fundet.";
                } catch (e) { if (current === version) results.textContent = `Søgning fejlede: ${e.message}`; }
            }, 250);
        });
        results.addEventListener("click", event => {
            const button = event.target.closest("[data-technique]");
            if (button) choose(matches.find(t => t.id === Number(button.dataset.technique)));
        });
        const api = { get value() { return value; }, choose };
        pickers.set(form, api); choose(null); return api;
    }
    const startPicker = picker(start);
    start.elements.systemAssetId.addEventListener("change", () => assessment(start, startPicker.value));
    function fields() { return `<div class="component-picker"></div><label>Likelihood<input name="likelihood" type="number" min="1" max="5" value="3" required></label><label>Kommentar<textarea name="comment" maxlength="4000" rows="1"></textarea></label><p data-existing class="muted"></p><button type="submit">Tilføj næste attack component</button>`; }
    async function renderSelected() {
        const c = component(selectedId), root = A.$("#selected-component");
        if (!c) { root.innerHTML = "<p>Vælg en Attack Component for at se indgående angreb og mulige næste assets.</p>"; return; }
        const id = c.id, next = await A.api(`attack-components/${id}/next-assets`);
        if (id !== selectedId) return;
        const incoming = connections.filter(link => link.toAttackComponentId === id), outgoing = connections.filter(link => link.fromAttackComponentId === id);
        const linkList = (links, incoming) => links.map(link => {
            const source = component(link.fromAttackComponentId), target = component(link.toAttackComponentId), relation = relations.find(r => r.id === link.systemAssetRelationId);
            return `<li>${A.esc(asset(source.systemAssetId)?.name)} —${A.esc(relation?.relationType)}→ ${A.esc(asset(target.systemAssetId)?.name)}<br><button type="button" data-select="${incoming ? source.id : target.id}">${A.esc(title(incoming ? source : target))}</button> <button type="button" class="delete-icon" data-delete-connection="${link.id}" aria-label="Slet forbindelse">×</button></li>`;
        }).join("");
        root.innerHTML = `<section><h2>${A.esc(title(c))}</h2><form id="component-assessment" data-component="${c.id}"><label>Likelihood<input name="likelihood" type="number" min="1" max="5" value="${c.likelihood}" required></label><label>Kommentar<textarea name="comment" maxlength="4000" rows="1">${A.esc(c.comment)}</textarea></label><button type="submit">Gem vurdering</button></form><h3>Angreb kan komme fra:</h3>${incoming.length ? `<ul class="attack-links">${linkList(incoming, true)}</ul>` : "<p>Ingen indgående forbindelser.</p>"}<h3>Angreb fortsætter til:</h3>${outgoing.length ? `<ul class="attack-links">${linkList(outgoing, false)}</ul>` : "<p>Ingen udgående forbindelser.</p>"}</section><section><h2>Mulige næste assets</h2><p>Kun direkte, udgående Asset Model-relationer vises. Vælg teknik og vurdering for det næste asset.</p><div class="cards next-components">${next.map(n => `<article><h3>${A.esc(n.asset.name)}</h3><p>${A.esc(asset(c.systemAssetId)?.name)} —${A.esc(n.relationType)}→ ${A.esc(n.asset.name)}</p><p class="muted">${A.esc(n.description)}</p><form class="next-component-form" data-source="${id}" data-relation="${n.systemAssetRelationId}" data-target="${n.asset.id}">${fields()}</form>${components.filter(t => t.systemAssetId === n.asset.id).map(t => `<button type="button" data-reuse="${t.id}">Brug ${A.esc(t.technique.techniqueId)} · Likelihood ${t.likelihood}</button>`).join("")}</article>`).join("") || "<p>Ingen udgående relationer. Tilføj relationer i <a href=\"/asset-model\">Asset Model</a>.</p>"}</div></section>`;
        root.querySelectorAll(".next-component-form").forEach(picker);
        root.querySelectorAll("textarea").forEach(areaSize);
    }
    async function load() {
        [components, connections, assets, surface, relations] = await Promise.all(["attack-components", "attack-connections", "system-assets", "attack-surface", "system-asset-relations"].map(path => A.api(path)));
        const previous = start.elements.systemAssetId.value, exposed = assets.filter(a => surface.some(s => s.systemAssetId === a.id && s.isExposed));
        start.elements.systemAssetId.innerHTML = A.option(exposed);
        if (exposed.some(a => String(a.id) === previous)) start.elements.systemAssetId.value = previous;
        start.querySelector("fieldset").disabled = !exposed.length; A.$("#start-empty").hidden = Boolean(exposed.length);
        assessment(start, startPicker.value);
        A.$("#component-list").innerHTML = components.length ? A.table(["System Asset", "ATT&CK Technique", "Startpunkt", "Likelihood", "Kommentar", ""], components.map(c => [A.esc(asset(c.systemAssetId)?.name), A.esc(`${c.technique.techniqueId} – ${c.technique.name}`), c.isInitial ? "Ja" : "–", c.likelihood, A.esc(c.comment), `<button type="button" data-select="${c.id}" aria-pressed="${c.id === selectedId}">Vælg</button> <button type="button" class="delete-icon" data-delete-component="${c.id}" aria-label="Slet komponent">×</button>`])) : "<p>Ingen Attack Components endnu.</p>";
        await renderSelected();
    }
    document.addEventListener("submit", event => {
        const form = event.target;
        if (!form.matches("#start-component-form, .next-component-form, #component-assessment")) return;
        event.preventDefault();
        A.run(async () => {
            const button = form.querySelector('[type="submit"]'); button.disabled = true;
            try {
                let result;
                if (form.id === "component-assessment") {
                    await A.api(`attack-components/${form.dataset.component}`, "PUT", { likelihood: Number(form.elements.likelihood.value), comment: form.elements.comment.value });
                } else {
                    const technique = pickers.get(form).value;
                    if (!technique) throw new Error("Vælg en ATT&CK-teknik fra kataloget.");
                    const body = { attackTechniqueId: technique.id, likelihood: Number(form.elements.likelihood.value), comment: form.elements.comment.value };
                    if (form === start) {
                        body.systemAssetId = Number(form.elements.systemAssetId.value); result = await A.api("attack-components", "POST", body);
                    } else {
                        body.systemAssetRelationId = Number(form.dataset.relation); result = await A.api(`attack-components/${form.dataset.source}/next`, "POST", body);
                    }
                    selectedId = result.component.id;
                }
                A.feedback(""); await load();
            } finally { button.disabled = false; }
        });
    });
    document.addEventListener("input", event => {
        if (!event.target.matches('#component-assessment input, #component-assessment textarea, .next-component-form textarea, #start-component-form textarea')) return;
        if (event.target.tagName === "TEXTAREA") areaSize(event.target);
        if (event.target.form.id === "component-assessment") {
            const c = component(Number(event.target.form.dataset.component));
            event.target.classList.toggle("unsaved", String(c[event.target.name]) !== event.target.value);
        }
    });
    document.addEventListener("click", event => {
        const button = event.target.closest("button"); if (!button) return;
        if (button.dataset.select) { selectedId = Number(button.dataset.select); A.run(load); }
        if (button.dataset.reuse) { const c = component(Number(button.dataset.reuse)); pickers.get(button.closest("article").querySelector("form")).choose(c.technique); }
        const resource = button.dataset.deleteComponent ? "attack-components" : button.dataset.deleteConnection ? "attack-connections" : null;
        if (resource && confirm(resource === "attack-components" ? "Slet komponenten og alle dens forbindelser og Control-valg?" : "Slet forbindelsen? Vurderingerne bevares.")) A.run(async () => {
            button.disabled = true;
            try { await A.api(`${resource}/${button.dataset.deleteComponent || button.dataset.deleteConnection}`, "DELETE"); await load(); }
            finally { button.disabled = false; }
        });
    });
    await load();
});
