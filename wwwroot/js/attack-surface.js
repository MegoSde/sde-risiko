RiskApp.run(async () => {
    const { api, esc, $ } = RiskApp;
    const exposures = { Internet: "Internet exposed", RemoteAccess: "Remote access", InternalNetwork: "Internal network", UserInteraction: "User interaction", ThirdParty: "Third party", Cloud: "Cloud", Physical: "Physical", Other: "Other" };
    const [assets, entries, relations] = await Promise.all([api("system-assets"), api("attack-surface"), api("system-asset-relations")]);
    const saved = new Map(entries.map(entry => [entry.systemAssetId, entry]));
    const networks = assets.filter(asset => asset.assetType === "Network");
    const placements = new Map(assets.map(asset => [asset.id, relations.filter(r => r.sourceSystemAssetId === asset.id && r.relationType === "LocatedIn").map(r => networks.find(n => n.id === r.targetSystemAssetId)).filter(Boolean)]));
    const queues = new Map();
    $("#surface-type").insertAdjacentHTML("beforeend", [...new Set(assets.map(a => a.assetType))].sort().map(t => `<option>${esc(t)}</option>`).join(""));
    $("#surface-network").insertAdjacentHTML("beforeend", networks.map(n => `<option value="${n.id}">${esc(n.name)}</option>`).join(""));
    $("#surface-assets").innerHTML = assets.map(asset => {
        const entry = saved.get(asset.id) || { isExposed: false, exposureType: "Other", description: "" };
        return `<article data-asset="${asset.id}"><h2>${esc(asset.name)}</h2><p class="muted">Type: ${esc(asset.assetType)}${placements.get(asset.id).length ? `<br>Placering: ${placements.get(asset.id).map(n => esc(n.name)).join(", ")}` : ""}</p>
        <label class="check"><input name="isExposed" type="checkbox" ${entry.isExposed ? "checked" : ""}>Attack Surface</label>
        <div data-exposure ${entry.isExposed ? "" : "hidden"}><label>Exposure<select name="exposureType">${Object.entries(exposures).map(([type, label]) => `<option value="${type}" ${type === entry.exposureType ? "selected" : ""}>${esc(label)}</option>`).join("")}</select></label>
        <label>Begrundelse<textarea name="description" maxlength="2000" rows="1">${esc(entry.description)}</textarea></label></div>
        <p data-status role="status" class="surface-status"></p><button type="button" data-retry hidden>Prøv at gemme igen</button></article>`;
    }).join("");
    const cards = [...document.querySelectorAll("[data-asset]")];
    function values(card) {
        return { systemAssetId: Number(card.dataset.asset), isExposed: card.querySelector('[name="isExposed"]').checked, exposureType: card.querySelector('[name="exposureType"]').value, description: card.querySelector('[name="description"]').value };
    }
    function resize(area) { area.style.height = "auto"; area.style.height = `${area.scrollHeight + 2}px`; }
    function dirty(card) {
        const current = values(card), previous = saved.get(current.systemAssetId) || { isExposed: false, exposureType: "Other", description: "" };
        let changed = false;
        for (const name of ["isExposed", "exposureType", "description"]) {
            const different = current[name] !== previous[name];
            card.querySelector(`[name="${name}"]`).classList.toggle("unsaved", different);
            changed ||= different;
        }
        card.classList.toggle("unsaved", changed);
    }
    function update() {
        $("#surface-total").textContent = assets.length;
        const exposed = [...saved.values()].filter(e => e.isExposed);
        $("#surface-selected").textContent = exposed.length;
        $("#surface-summary").innerHTML = Object.entries(exposures).map(([type, label]) => `<div><dt>${label}</dt><dd>${exposed.filter(e => e.exposureType === type).length}</dd></div>`).join("");
        let count = 0;
        for (const card of cards) {
            const id = Number(card.dataset.asset), asset = assets.find(a => a.id === id), locations = placements.get(id);
            const network = $("#surface-network").value;
            card.hidden = Boolean(($("#surface-type").value && asset.assetType !== $("#surface-type").value) || (network && (network === "none" ? locations.length > 0 : !locations.some(n => String(n.id) === network))) || ($("#surface-only").checked && !saved.get(id)?.isExposed && !card.classList.contains("unsaved")));
            if (!card.hidden) { count++; resize(card.querySelector("textarea")); }
        }
        $("#surface-count").textContent = `${count} af ${assets.length} assets vises`;
        $("#surface-empty").hidden = count !== 0;
    }
    function save(card) {
        const body = values(card), id = body.systemAssetId, status = card.querySelector("[data-status]"), retry = card.querySelector("[data-retry]");
        const task = (queues.get(id) || Promise.resolve()).then(async () => {
            status.textContent = "Gemmer…"; status.classList.remove("error"); retry.hidden = true;
            try {
                const previous = saved.get(id);
                const row = await api(previous ? `attack-surface/${previous.id}` : "attack-surface", previous ? "PUT" : "POST", body);
                saved.set(id, row); status.textContent = "";
            } catch (error) {
                status.textContent = `Kunne ikke gemme: ${error.message}`; status.classList.add("error"); retry.hidden = false;
            }
            dirty(card); update();
        });
        queues.set(id, task);
    }
    $("#surface-assets").addEventListener("input", event => {
        const card = event.target.closest("[data-asset]");
        if (!card || !event.target.name) return;
        dirty(card);
        if (event.target.tagName === "TEXTAREA") resize(event.target);
    });
    $("#surface-assets").addEventListener("change", event => {
        const card = event.target.closest("[data-asset]");
        if (!card || !event.target.name) return;
        card.querySelector("[data-exposure]").hidden = !values(card).isExposed;
        resize(card.querySelector("textarea")); dirty(card); save(card);
    });
    $("#surface-assets").addEventListener("click", event => {
        if (event.target.matches("[data-retry]")) save(event.target.closest("[data-asset]"));
    });
    for (const selector of ["#surface-type", "#surface-network", "#surface-only"]) $(selector).addEventListener("change", update);
    update();
});
