using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace CloudQATask1
{
    class Program
    {
        static int Main(string[] args)
        {
            string rawUrl = args.Length > 0 ? args[0] : "https://app.cloudqa.io/home/AutomationPracticeForm";
            string url = NormalizeUrl(rawUrl);

            string firstNameValue = args.Length > 1 ? args[1] : "Saanvi";
            string genderValue = args.Length > 2 ? args[2] : "Female";
            string preferredState = args.Length > 3 ? args[3] : "India";

            var options = new ChromeOptions();
            if (Environment.GetEnvironmentVariable("HEADLESS") == "1")
            {
                options.AddArgument("--headless=new");
            }

            int passed = 0;
            int failed = 0;

            using IWebDriver driver = new ChromeDriver(options);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

            try
            {
                Console.WriteLine($"Target URL: {url}");
                driver.Navigate().GoToUrl(url);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FAIL] Navigation error: {ex.Message}");
                return 1;
            }

            if (RunTest("First Name (Text Box)", () => TestFirstName(driver, wait, firstNameValue)))
                passed++;
            else
                failed++;

            if (RunTest("Gender (Radio Button)", () => TestGender(driver, wait, genderValue)))
                passed++;
            else
                failed++;

            if (RunTest("State (Dropdown)", () => TestState(driver, wait, preferredState)))
                passed++;
            else
                failed++;

            Console.WriteLine($"\nSummary: {passed} passed, {failed} failed.");
            return failed == 0 ? 0 : 1;
        }

        static string NormalizeUrl(string input)
        {
            string trimmed = input.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (System.IO.File.Exists(trimmed))
            {
                return new Uri(System.IO.Path.GetFullPath(trimmed)).AbsoluteUri;
            }

            return "https://" + trimmed;
        }

        static bool RunTest(string testName, Action testAction)
        {
            try
            {
                testAction();
                Console.WriteLine($"[PASS] {testName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FAIL] {testName}: {ex.Message}");
                return false;
            }
        }

        static void TestFirstName(IWebDriver driver, WebDriverWait wait, string expectedValue)
        {
            IWebElement input = LocateInPageOrFrames(driver, wait, () =>
                FindBestTextInput(driver, "First Name", new[] { "fname", "firstname", "given-name" }));

            ScrollIntoView(driver, input);
            input.Clear();
            input.SendKeys(expectedValue);

            string actualValue = input.GetAttribute("value") ?? string.Empty;
            if (!string.Equals(actualValue, expectedValue, StringComparison.Ordinal))
            {
                throw new Exception($"Expected '{expectedValue}', but input value was '{actualValue}'.");
            }
        }

        static void TestGender(IWebDriver driver, WebDriverWait wait, string optionLabel)
        {
            RadioMatch match = LocateInPageOrFrames(driver, wait, () =>
                FindBestRadioOption(driver, "Gender", optionLabel));

            IWebElement radioInput = match.Radio;
            IWebElement? clickableLabel = match.Label;

            ScrollIntoView(driver, radioInput);

            try
            {
                if (radioInput.Displayed && radioInput.Enabled)
                {
                    radioInput.Click();
                }
                else if (clickableLabel != null)
                {
                    clickableLabel.Click();
                }
            }
            catch (WebDriverException)
            {
                if (clickableLabel != null)
                {
                    clickableLabel.Click();
                }
                else
                {
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", radioInput);
                }
            }

            if (!radioInput.Selected && clickableLabel != null)
            {
                try { clickableLabel.Click(); } catch (WebDriverException) { }
            }

            if (!radioInput.Selected)
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", radioInput);
            }

            if (!radioInput.Selected)
            {
                throw new Exception($"Radio option '{optionLabel}' was clicked, but Selected is still false.");
            }
        }

        static void TestState(IWebDriver driver, WebDriverWait wait, string preferredOption)
        {
            IWebElement dropdown = LocateInPageOrFrames(driver, wait, () =>
                FindBestDropdown(driver, "State"));

            ScrollIntoView(driver, dropdown);

            if (string.Equals(dropdown.TagName, "select", StringComparison.OrdinalIgnoreCase))
            {
                var select = new SelectElement(dropdown);
                IWebElement? targetOption = select.Options.FirstOrDefault(o =>
                    IsUsableOption(o) &&
                    (string.Equals(o.Text.Trim(), preferredOption, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(o.GetAttribute("value")?.Trim(), preferredOption, StringComparison.OrdinalIgnoreCase)));

                targetOption ??= select.Options.FirstOrDefault(IsUsableOption);

                if (targetOption == null)
                {
                    throw new Exception("State <select> contained no selectable options.");
                }

                string expectedText = targetOption.Text.Trim();
                select.SelectByText(targetOption.Text);

                string actualText = select.SelectedOption.Text.Trim();
                if (!string.Equals(actualText, expectedText, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Expected '{expectedText}', but selected option is '{actualText}'.");
                }
            }
            else
            {
                dropdown.Click();

                IWebElement optionElement = wait.Until(d =>
                {
                    var items = CollectElementsIncludingShadow(d, "[role='option'], ul.dropdown-menu li, .dropdown-item, li")
                        .Where(IsInteractable)
                        .ToList();

                    return items.FirstOrDefault(i => string.Equals(i.Text.Trim(), preferredOption, StringComparison.OrdinalIgnoreCase))
                        ?? items.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Text) && !IsPlaceholderText(i.Text));
                }) ?? throw new Exception("Custom State dropdown opened, but no visible options were found.");

                string expectedText = optionElement.Text.Trim();
                optionElement.Click();
            }
        }

        static T LocateInPageOrFrames<T>(IWebDriver driver, WebDriverWait wait, Func<T?> finder) where T : class
        {
            return wait.Until(d =>
            {
                d.SwitchTo().DefaultContent();
                T? result = SearchFramesRecursive(d, finder, 0, 3);
                if (result != null) return result;

                d.SwitchTo().DefaultContent();
                return null;
            }) ?? throw new NoSuchElementException("Timed out waiting for matching element.");
        }

        static T? SearchFramesRecursive<T>(IWebDriver driver, Func<T?> finder, int currentDepth, int maxDepth) where T : class
        {
            T? result = finder();
            if (result != null) return result;

            if (currentDepth >= maxDepth) return null;

            var frames = driver.FindElements(By.TagName("iframe"));
            for (int i = 0; i < frames.Count; i++)
            {
                try
                {
                    driver.SwitchTo().Frame(i);
                    result = SearchFramesRecursive(driver, finder, currentDepth + 1, maxDepth);
                    if (result != null) return result;
                    driver.SwitchTo().ParentFrame();
                }
                catch (WebDriverException)
                {
                    try { driver.SwitchTo().ParentFrame(); } catch { driver.SwitchTo().DefaultContent(); }
                }
            }

            return null;
        }

        static IWebElement? FindBestTextInput(IWebDriver driver, string labelText, string[] attributeHints)
        {
            var blockedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "hidden", "radio", "checkbox", "submit", "reset", "button", "file", "password", "image", "color", "range", "date"
            };

            var candidates = CollectElementsIncludingShadow(driver, "input, textarea")
                .Where(IsInteractable)
                .Where(el =>
                {
                    if (string.Equals(el.TagName, "textarea", StringComparison.OrdinalIgnoreCase)) return false;
                    string type = el.GetAttribute("type") ?? "text";
                    return !blockedTypes.Contains(type);
                });

            IWebElement? best = null;
            int bestScore = 0;

            foreach (var input in candidates)
            {
                int score = ScoreElementAgainstLabel(driver, input, labelText, attributeHints);

                string placeholder = input.GetAttribute("placeholder") ?? string.Empty;
                if (HasExactPhrase(placeholder, labelText))
                    score += 80;
                else if (HasWord(placeholder, labelText))
                    score += 40;
                else if (HasWord(placeholder, "name") && score > 0)
                    score += 10;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = input;
                }
            }

            return best;
        }

        static RadioMatch? FindBestRadioOption(IWebDriver driver, string groupLabel, string optionText)
        {
            var radios = CollectElementsIncludingShadow(driver, "input[type='radio']");

            RadioMatch? best = null;
            int bestScore = 0;

            foreach (var radio in radios)
            {
                var meta = GetRadioMetadata(driver, radio);
                if (!IsInteractable(radio) && (meta.ClickableLabel == null || !IsInteractable(meta.ClickableLabel)))
                    continue;

                int score = 0;

                foreach (string candidateText in meta.OptionTexts)
                {
                    if (HasExactPhrase(candidateText, optionText))
                        score = Math.Max(score, 300);
                    else if (HasWord(candidateText, optionText))
                        score = Math.Max(score, 120);
                }

                string valueAttr = radio.GetAttribute("value") ?? string.Empty;
                string idAttr = radio.GetAttribute("id") ?? string.Empty;

                if (HasExactPhrase(valueAttr, optionText))
                    score += 90;
                else if (HasExactPhrase(idAttr, optionText))
                    score += 70;

                if (score == 0)
                    continue;

                string nameAttr = radio.GetAttribute("name") ?? string.Empty;
                if (HasWord(nameAttr, groupLabel))
                    score += 40;

                foreach (string grpText in meta.GroupTexts)
                {
                    if (HasExactPhrase(grpText, groupLabel))
                    {
                        score += 60;
                        break;
                    }
                    if (HasWord(grpText, groupLabel))
                    {
                        score += 35;
                        break;
                    }
                }

                if (meta.InShadowOrSlot)
                    score -= 25;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new RadioMatch { Radio = radio, Label = meta.ClickableLabel };
                }
            }

            return best;
        }

        static IWebElement? FindBestDropdown(IWebDriver driver, string labelText)
        {
            string[] hints = { labelText.ToLowerInvariant() };

            var selects = CollectElementsIncludingShadow(driver, "select").Where(IsInteractable);
            IWebElement? best = null;
            int bestScore = 0;

            foreach (var select in selects)
            {
                int score = ScoreElementAgainstLabel(driver, select, labelText, hints);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = select;
                }
            }

            if (best != null)
                return best;

            var customTriggers = CollectElementsIncludingShadow(driver, "[role='combobox'], [role='listbox'], .dropdown-toggle")
                .Where(IsInteractable);

            foreach (var trigger in customTriggers)
            {
                int score = ScoreElementAgainstLabel(driver, trigger, labelText, hints);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = trigger;
                }
            }

            return best;
        }

        static int ScoreElementAgainstLabel(IWebDriver driver, IWebElement element, string targetLabel, string[] attributeHints)
        {
            var meta = GetFieldMetadata(driver, element);
            int score = 0;

            foreach (string text in meta.LabelTexts)
            {
                if (HasExactPhrase(text, targetLabel))
                    score = Math.Max(score, 300);
                else if (HasWord(text, targetLabel))
                    score = Math.Max(score, 90);
            }

            string id = element.GetAttribute("id") ?? string.Empty;
            string nameAttr = element.GetAttribute("name") ?? string.Empty;
            string ariaLabel = element.GetAttribute("aria-label") ?? string.Empty;

            if (HasExactPhrase(nameAttr, targetLabel) || HasExactPhrase(ariaLabel, targetLabel))
                score += 60;
            else if (HasWord(nameAttr, targetLabel) || HasWord(ariaLabel, targetLabel))
                score += 30;

            foreach (string hint in attributeHints)
            {
                if (string.Equals(id, hint, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nameAttr, hint, StringComparison.OrdinalIgnoreCase))
                {
                    score += 35;
                }
            }

            if (score > 0 && meta.InShadowOrSlot)
                score -= 25;

            return score;
        }

        static IReadOnlyList<IWebElement> CollectElementsIncludingShadow(IWebDriver driver, string cssSelector)
        {
            const string script = @"
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

            var raw = ((IJavaScriptExecutor)driver).ExecuteScript(script, cssSelector) as IReadOnlyCollection<object>;
            return raw?.OfType<IWebElement>().ToList() ?? new List<IWebElement>();
        }

        static (List<string> LabelTexts, bool InShadowOrSlot) GetFieldMetadata(IWebDriver driver, IWebElement element)
        {
            const string script = @"
                const el = arguments[0];
                const texts = [];
                const root = el.getRootNode();
                let inShadowOrSlot = (root instanceof ShadowRoot) || !!el.closest('[slot]');

                const addText = (t) => {
                    if (t && typeof t === 'string' && t.trim()) texts.push(t.trim());
                };

                if (el.id && root.querySelectorAll) {
                    root.querySelectorAll(`[for='${CSS.escape(el.id)}']`).forEach(l => addText(l.textContent));
                }

                const slotParent = el.closest('[slot]');
                if (slotParent) {
                    const slotName = slotParent.getAttribute('slot');
                    const host = slotParent.parentElement;
                    if (host && host.shadowRoot && slotName) {
                        const slotEl = host.shadowRoot.querySelector(`slot[name='${CSS.escape(slotName)}']`);
                        if (slotEl && slotEl.parentElement) {
                            slotEl.parentElement.querySelectorAll('label, span, div, p, th, td, legend').forEach(l => addText(l.textContent));
                            addText(slotEl.parentElement.textContent);
                        }
                    }
                }

                const wrappingLabel = el.closest('label');
                if (wrappingLabel) addText(wrappingLabel.textContent);

                let curr = el;
                for (let depth = 0; depth < 4 && curr && curr !== document.body; depth++) {
                    let prev = curr.previousElementSibling;
                    while (prev) {
                        if (['LABEL', 'SPAN', 'DIV', 'P', 'TH', 'TD', 'LEGEND', 'B', 'STRONG'].includes(prev.tagName)) {
                            addText(prev.textContent);
                        }
                        prev = prev.previousElementSibling;
                    }
                    const parent = curr.parentElement;
                    if (parent) {
                        for (const child of parent.children) {
                            if (child === curr || child.contains(el)) break;
                            if (['LABEL', 'SPAN', 'LEGEND', 'TH'].includes(child.tagName) ||
                                (child.classList && child.classList.contains('control-label'))) {
                                addText(child.textContent);
                            }
                        }
                    }
                    curr = parent;
                }

                return [texts, inShadowOrSlot];";

            var res = ((IJavaScriptExecutor)driver).ExecuteScript(script, element) as IReadOnlyList<object>;
            var list = (res?[0] as IReadOnlyCollection<object>)?.Select(o => o?.ToString() ?? "").ToList() ?? new List<string>();
            bool inShadow = res != null && res.Count > 1 && res[1] is bool b && b;
            return (list, inShadow);
        }

        static (List<string> OptionTexts, List<string> GroupTexts, IWebElement? ClickableLabel, bool InShadowOrSlot) GetRadioMetadata(IWebDriver driver, IWebElement radio)
        {
            const string script = @"
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

            var res = ((IJavaScriptExecutor)driver).ExecuteScript(script, radio) as IReadOnlyList<object>;
            var optList = (res?[0] as IReadOnlyCollection<object>)?.Select(o => o?.ToString() ?? "").ToList() ?? new List<string>();
            var grpList = (res?[1] as IReadOnlyCollection<object>)?.Select(o => o?.ToString() ?? "").ToList() ?? new List<string>();
            var clickable = res != null && res.Count > 2 ? res[2] as IWebElement : null;
            bool inShadow = res != null && res.Count > 3 && res[3] is bool b && b;
            return (optList, grpList, clickable, inShadow);
        }

        static void ScrollIntoView(IWebDriver driver, IWebElement element)
        {
            try
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", element);
            }
            catch (WebDriverException) { }
        }

        static bool IsInteractable(IWebElement el)
        {
            try
            {
                return el.Displayed && el.Enabled;
            }
            catch (WebDriverException)
            {
                return false;
            }
        }

        static bool IsUsableOption(IWebElement option)
        {
            string text = option.Text.Trim();
            return option.Enabled && !string.IsNullOrWhiteSpace(text) && !IsPlaceholderText(text);
        }

        static bool IsPlaceholderText(string text)
        {
            string t = text.Trim();
            return t.StartsWith("--") ||
                   t.StartsWith("Select", StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("Choose", StringComparison.OrdinalIgnoreCase);
        }

        static bool HasExactPhrase(string input, string target)
        {
            string cleanInput = Regex.Replace(input, @"[^\w\s]", " ");
            cleanInput = Regex.Replace(cleanInput, @"\s+", " ").Trim();

            string cleanTarget = Regex.Replace(target, @"[^\w\s]", " ");
            cleanTarget = Regex.Replace(cleanTarget, @"\s+", " ").Trim();

            return !string.IsNullOrEmpty(cleanInput) &&
                   string.Equals(cleanInput, cleanTarget, StringComparison.OrdinalIgnoreCase);
        }

        static bool HasWord(string input, string targetWords)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(targetWords))
                return false;

            string pattern = @"\b" + string.Join(@"\s+", targetWords.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape)) + @"\b";
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
        }
    }

    public class RadioMatch
    {
        public IWebElement Radio { get; set; } = null!;
        public IWebElement? Label { get; set; }
    }
}