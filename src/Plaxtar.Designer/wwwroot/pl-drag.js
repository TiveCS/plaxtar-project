// Plaxtar Designer JS drag layer (#23, ADR 0007). Owns the per-mousemove drop
// preview so the Blazor Server circuit isn't hit on every dragover; .NET is called
// exactly once, on drop, with (targetId, pos). Payload (which node/new item is being
// dragged) is set server-side by the existing @ondragstart handlers; here we only
// resolve the target zone and draw the insertion line.

let dotnet = null;
let dragging = false;
let draggedId = null;     // node id being moved, or null for a new-node (palette) drag
let line = null;
let current = null;       // { id, pos }  chosen target for the pending drop

function ensureLine() {
    if (!line) {
        line = document.createElement('div');
        line.className = 'pl-drop-line';
        line.style.cssText =
            'position:fixed;height:0;border-top:2px solid #5b6cf0;z-index:99999;' +
            'pointer-events:none;display:none;border-radius:2px;' +
            'box-shadow:0 0 0 1px rgba(91,108,240,.35)';
        document.body.appendChild(line);
    }
    return line;
}

function clearViz() {
    if (line) line.style.display = 'none';
    document.querySelectorAll('.pl-into').forEach(e => e.classList.remove('pl-into'));
    current = null;
}

function zoneFor(el, y) {
    const r = el.getBoundingClientRect();
    const rel = r.height > 0 ? (y - r.top) / r.height : 0.5;
    if (el.hasAttribute('data-container')) {
        if (rel < 0.30) return { pos: 'before', r };
        if (rel > 0.70) return { pos: 'after', r };
        return { pos: 'into', r };
    }
    return { pos: rel < 0.5 ? 'before' : 'after', r };
}

function onStart(e) {
    const src = e.target.closest ? e.target.closest('[draggable="true"]') : null;
    if (!src) return;
    dragging = true;
    const nodeEl = e.target.closest('[data-node-id]');
    draggedId = nodeEl ? nodeEl.getAttribute('data-node-id') : null;   // null = new item
    try { e.dataTransfer.effectAllowed = draggedId ? 'move' : 'copy'; } catch (_) {}
}

function onOver(e) {
    if (!dragging) return;
    const scope = e.target.closest ? e.target.closest('.canvas, .dock-left') : null;
    if (!scope) { clearViz(); return; }

    const el = e.target.closest('[data-node-id]');
    if (!el) {
        // Empty area inside the canvas → append to the root.
        if (e.target.closest('.canvas')) { e.preventDefault(); current = { id: '', pos: 'into' }; if (line) line.style.display = 'none'; }
        else clearViz();
        return;
    }

    const id = el.getAttribute('data-node-id');
    // Reject dropping a node onto itself or its own descendant.
    if (draggedId) {
        if (id === draggedId) { clearViz(); return; }
        const srcEl = document.querySelector('[data-node-id="' + CSS.escape(draggedId) + '"]');
        if (srcEl && srcEl.contains(el)) { clearViz(); return; }
    }

    e.preventDefault();
    const z = zoneFor(el, e.clientY);
    current = { id, pos: z.pos };

    document.querySelectorAll('.pl-into').forEach(x => x.classList.remove('pl-into'));
    if (z.pos === 'into') {
        if (line) line.style.display = 'none';
        el.classList.add('pl-into');
    } else {
        const L = ensureLine();
        L.style.display = 'block';
        L.style.left = z.r.left + 'px';
        L.style.width = z.r.width + 'px';
        L.style.top = (z.pos === 'before' ? z.r.top : z.r.bottom) - 1 + 'px';
    }
}

function onDrop(e) {
    if (!dragging) return;
    e.preventDefault();
    const t = current;
    clearViz();
    dragging = false;
    const id = draggedId;
    draggedId = null;
    if (t && dotnet) dotnet.invokeMethodAsync('OnDropAt', t.id, t.pos);
}

function onEnd() { dragging = false; draggedId = null; clearViz(); }

// --- Keyboard shortcuts (#28) --------------------------------------------------
function inField() {
    const el = document.activeElement;
    return !!(el && el.closest && el.closest('input, textarea, select, [contenteditable="true"], [contenteditable=""]'));
}

function onKey(e) {
    const ctrl = e.ctrlKey || e.metaKey;
    let cmd = null;
    if (e.key === 'Delete') { if (inField()) return; cmd = 'delete'; }
    else if (ctrl && !e.shiftKey && e.key.toLowerCase() === 's') cmd = 'save';
    else if (ctrl && e.key.toLowerCase() === 'g') cmd = 'preview';
    else if (ctrl && e.key.toLowerCase() === 'b') cmd = 'togglePanels';
    else if (ctrl && e.key.toLowerCase() === 'z') { if (inField()) return; cmd = e.shiftKey ? 'redo' : 'undo'; }
    else if (ctrl && e.key.toLowerCase() === 'y') { if (inField()) return; cmd = 'redo'; }
    if (!cmd) return;
    e.preventDefault();
    if (dotnet) dotnet.invokeMethodAsync('OnShortcut', cmd);
}

export function init(ref) {
    dotnet = ref;
    document.addEventListener('dragstart', onStart, true);
    document.addEventListener('dragover', onOver, true);
    document.addEventListener('drop', onDrop, true);
    document.addEventListener('dragend', onEnd, true);
    document.addEventListener('keydown', onKey, true);
}

export function dispose() {
    document.removeEventListener('dragstart', onStart, true);
    document.removeEventListener('dragover', onOver, true);
    document.removeEventListener('drop', onDrop, true);
    document.removeEventListener('dragend', onEnd, true);
    document.removeEventListener('keydown', onKey, true);
    if (line) { line.remove(); line = null; }
    dotnet = null;
}
