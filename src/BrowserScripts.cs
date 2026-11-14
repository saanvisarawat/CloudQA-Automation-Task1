namespace CloudQATask1
{

    public static class BrowserScripts
    {
       
        public const string QuerySelectorAllWithShadow = @"
                const selector = arguments[0];
                const results = [];
                function collect(root) {
                    if (!root) return;
                    try {
                        root.querySelectorAll(selector).forEach(el => results.push(el));
                    } catch (e) {}
                    const all = root.querySelectorAll ? root.querySelectorAll('*') : [];
                    for (const el of all) {
                        if (el.shadowRoot) collect(el.shadowRoot);
                    }
                }
                collect(document);
                return results;";

        
        public const string DescribeElement = @"
                const e = arguments[0];
                let s = '<' + e.tagName.toLowerCase();
                for (const n of ['id', 'name', 'type', 'value', 'placeholder', 'aria-label']) {
                    if (e.getAttribute(n)) s += ' ' + n + '=' + e.getAttribute(n);
                }
                return s + '>';";

       
        public const string GetFieldMetadata = @"
                const el = arguments[0];
                const texts = [];
                const root = el.getRootNode();
                const inShadowOrSlot = (root instanceof ShadowRoot) || !!el.closest('[slot]');

                const addText = (t) => {
                    if (t && typeof t === 'string' && t.trim()) texts.push(t.trim());
                };

                if (el.id && root.querySelectorAll) {
                    root.querySelectorAll(`[for='${CSS.escape(el.id)}']`).forEach(l => addText(l.textContent));
                }

                const labelledBy = el.getAttribute('aria-labelledby');
                if (labelledBy && root.getElementById) {
                    labelledBy.split(/\s+/).forEach(id => {
                        const n = root.getElementById(id);
                        if (n) addText(n.textContent);
                    });
                }

                const wrappingLabel = el.closest('label');
                if (wrappingLabel) {
                    const copy = wrappingLabel.cloneNode(true);
                    copy.querySelectorAll('input, select, textarea, option').forEach(n => n.remove());
                    addText(copy.textContent);
                }

                const LABELISH = ['LABEL', 'SPAN', 'DIV', 'P', 'TH', 'TD', 'LEGEND', 'B', 'STRONG'];
                let curr = el;
                let found = false;
                for (let depth = 0; depth < 4 && curr && curr !== document.body && !found; depth++) {
                    let prev = curr.previousElementSibling;
                    while (prev) {
                        if (prev.matches('input, select, textarea') || prev.querySelector('input, select, textarea')) break;
                        if (LABELISH.includes(prev.tagName) && prev.textContent.trim()) {
                            addText(prev.textContent);
                            found = true;
                            break;
                        }
                        prev = prev.previousElementSibling;
                    }
                    curr = curr.parentElement;
                }

                return [texts, inShadowOrSlot];";

        
        public const string GetRadioMetadata = @"
                const r = arguments[0];
                const optTexts = [];
                const grpTexts = [];
                let clickable = null;
                const root = r.getRootNode();
                let inShadowOrSlot = (root instanceof ShadowRoot) || !!r.closest('[slot]');

                const addOpt = (t) => { if (t && t.trim()) optTexts.push(t.trim()); };
                const addGrp = (t) => { if (t && t.trim()) grpTexts.push(t.trim()); };

                if (r.id && root.querySelectorAll) {
                    const forMatches = root.querySelectorAll(`label[for='${CSS.escape(r.id)}'], span[for='${CSS.escape(r.id)}']`);
                    forMatches.forEach(m => {
                        if (m.textContent.trim().toLowerCase() === (r.value || '').toLowerCase() || !clickable) {
                            clickable = clickable || m;
                        }
                        addOpt(m.textContent);
                    });
                }

                const wrapLabel = r.closest('label');
                if (wrapLabel) {
                    clickable = clickable || wrapLabel;
                    addOpt(wrapLabel.textContent);
                }

                if (r.getAttribute('role') === 'radio') {
                    clickable = clickable || r;
                    addOpt(r.textContent);
                }

                const ariaGroup = r.closest('[role=radiogroup], [role=group]');
                if (ariaGroup) {
                    addGrp(ariaGroup.getAttribute('aria-label'));
                    const ids = ariaGroup.getAttribute('aria-labelledby');
                    if (ids && root.getElementById) {
                        ids.split(/\s+/).forEach(id => { const n = root.getElementById(id); if (n) addGrp(n.textContent); });
                    }
                }

                if (r.nextElementSibling && ['LABEL', 'SPAN'].includes(r.nextElementSibling.tagName)) {
                    clickable = clickable || r.nextElementSibling;
                    addOpt(r.nextElementSibling.textContent);
                }
                if (r.nextSibling && r.nextSibling.nodeType === Node.TEXT_NODE) {
                    addOpt(r.nextSibling.textContent);
                }

                let curr = r.parentElement;
                for (let d = 0; d < 4 && curr && curr !== document.body; d++) {
                    for (const child of curr.children) {
                        if (child.contains(r)) break;
                        if (['LABEL', 'LEGEND', 'SPAN', 'DIV', 'P'].includes(child.tagName)) {
                            addGrp(child.textContent);
                        }
                    }
                    curr = curr.parentElement;
                }

                return [optTexts, grpTexts, clickable, inShadowOrSlot];";
    }
}