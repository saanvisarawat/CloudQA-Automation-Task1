namespace CloudQATask1
{
    public static class BrowserScripts
    {
        public const string QuerySelectorAllWithShadow = @"
            const selector = arguments[0];
            const results = [];
            function walk(root) {
                if (!root) return;
                try { root.querySelectorAll(selector).forEach(el => results.push(el)); } catch(e){}
                root.querySelectorAll('*').forEach(el => {
                    if (el.shadowRoot) walk(el.shadowRoot);
                });
            }
            walk(document);
            return results;";

        public const string GetElementMetadata = @"
            const el = arguments[0];
            const root = el.getRootNode();
            const meta = { labels: '', group: '', attributes: '', isShadow: root instanceof ShadowRoot, id: el.id || '', associatedLabel: null };
            
            
            if (el.id) {
                const explicit = root.querySelector(`[for='${CSS.escape(el.id)}']`);
                if (explicit) { meta.labels += explicit.textContent + ' '; meta.associatedLabel = explicit; }
            }
            const wrapper = el.closest('label');
            if (wrapper) { meta.labels += wrapper.textContent + ' '; meta.associatedLabel = wrapper; }
            
            
            if (el.nextSibling && el.nextSibling.nodeType === Node.TEXT_NODE) {
                meta.labels += el.nextSibling.textContent + ' ';
            }

            
            const fieldset = el.closest('fieldset');
            if (fieldset) {
                const legend = fieldset.querySelector('legend');
                if (legend) meta.group += legend.textContent + ' ';
            }

            
            meta.attributes += (el.name || '') + ' ' + (el.getAttribute('placeholder') || '') + ' ' + (el.getAttribute('aria-label') || '');
            
            return meta;";
    }
}