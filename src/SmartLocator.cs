using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace CloudQATask1
{
    // One located thing plus how well it matched. RunnerUp = rank of the second best candidate in the same page or frame.
    public record Found<T>(T Item, int Rank, int RunnerUp) where T : class;

    public class RadioMatch
    {
        public IWebElement Radio { get; set; } = null!;
        public IWebElement? Label { get; set; }
    }

    // What the last test chose and why. Printed next to [PASS].
    public static class Log
    {
        public static string? Last;
        public static void Add(string text) => Last = Last == null ? text : Last + " | " + text;
    }

    public static class SmartLocator
    {
        const int MaxFrameDepth = 3;
        static readonly TimeSpan SettleWindow = TimeSpan.FromSeconds(2);

        // ------------------------------------------------------------------ ranking
        // Rank = match * 1_000_000 + group * 100_000 + light * 10_000 + extras (< 1000).
        //   match: 3 = exact label / option text, 2 = exact name / aria-label / placeholder / value, 1 = partial, 0 = not a candidate
        //   group: radios only. 2 = group label is exactly the group name, 1 = contains it
        //   light: 1 = normal page, 0 = inside a shadow root (only breaks ties)
        // Each tier is worth more than everything below it, so a lower tier can never outvote a higher one.

        static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["first name"] = new[] { "given name", "forename", "fname" },
            ["state"] = new[] { "province", "region" },
            ["gender"] = new[] { "sex" },
            ["female"] = new[] { "f", "woman" },
        };

        static readonly HashSet<string> BlockedInputTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "hidden", "radio", "checkbox", "submit", "reset", "button", "file", "password", "image", "color", "range", "date"
        };

        static int Pack(int match, int group, int light, int extras) =>
            match * 1_000_000 + group * 100_000 + light * 10_000 + Math.Min(extras, 999);

        static int MatchOf(int rank) => rank / 1_000_000;

        static List<string> NamesFor(string target)
        {
            var names = new List<string> { target };
            if (Synonyms.TryGetValue(target, out var extra)) names.AddRange(extra);
            return names;
        }

        // Label or option text: 3 = exact phrase, 1 = phrase appears as whole words, 0 = no match.
        static int TextLevel(string text, List<string> names)
        {
            int level = 0;
            foreach (string name in names)
            {
                if (HasExactPhrase(text, name)) return 3;
                if (name.Length >= 3 && HasWord(text, name)) level = 1;
            }
            return level;
        }

        // name / aria-label / placeholder: 2 = exact phrase, 1 = whole words, 0 = no match.
        static int AttrLevel(string text, List<string> names)
        {
            int level = 0;
            foreach (string name in names)
            {
                if (HasExactPhrase(text, name)) return 2;
                if (name.Length >= 3 && HasWord(text, name)) level = 1;
            }
            return level;
        }

        static string Attr(IWebElement element, string name) => element.GetAttribute(name) ?? string.Empty;

        static Found<T>? Pick<T>(List<(int Rank, T Item)> candidates) where T : class
        {
            T? best = null;
            int bestRank = 0;
            int runnerUp = 0;

            foreach (var (rank, item) in candidates)
            {
                if (rank <= 0) continue;

                if (best == null || rank > bestRank)
                {
                    if (best != null) runnerUp = Math.Max(runnerUp, bestRank);
                    best = item;
                    bestRank = rank;
                }
                else
                {
                    runnerUp = Math.Max(runnerUp, rank);
                }
            }

            return best == null ? null : new Found<T>(best, bestRank, runnerUp);
        }

        static int ScoreField(IWebDriver driver, IWebElement element, string targetLabel, string[] attributeHints)
        {
            var meta = GetFieldMetadata(driver, element);
            var names = NamesFor(targetLabel);
            int match = 0;
            int extras = 0;

            foreach (string text in meta.LabelTexts)
                match = Math.Max(match, TextLevel(text, names));

            string id = Attr(element, "id");
            string nameAttr = Attr(element, "name");
            string autocomplete = Attr(element, "autocomplete");

            foreach (string attr in new[] { nameAttr, Attr(element, "aria-label"), Attr(element, "placeholder") })
            {
                int level = AttrLevel(attr, names);
                if (level == 2) extras += 5;
                match = Math.Max(match, level);
            }

            foreach (string hint in attributeHints)
            {
                if (string.Equals(id, hint, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nameAttr, hint, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(autocomplete, hint, StringComparison.OrdinalIgnoreCase))
                {
                    match = Math.Max(match, 1);
                    extras += 10;
                }
            }

            if (match == 0) return 0;
            return Pack(match, 0, meta.InShadowOrSlot ? 0 : 1, extras);
        }

        static int ScoreRadio(IWebElement radio, List<string> optionTexts, List<string> groupTexts, bool inShadow,
            string groupLabel, string optionText)
        {
            var optionNames = NamesFor(optionText);
            var groupNames = NamesFor(groupLabel);
            int match = 0;
            int extras = 0;

            foreach (string text in optionTexts)
                match = Math.Max(match, TextLevel(text, optionNames));

            string value = Attr(radio, "value");
            string id = Attr(radio, "id");
            if (optionNames.Any(n => HasExactPhrase(value, n)))
            {
                match = Math.Max(match, 2);
                extras += 10;
            }
            else if (optionNames.Any(n => HasExactPhrase(id, n)))
            {
                match = Math.Max(match, 2);
                extras += 5;
            }

            if (match == 0) return 0;

            int group = AttrLevel(Attr(radio, "name"), groupNames);
            foreach (string text in groupTexts)
            {
                int level = TextLevel(text, groupNames);
                group = Math.Max(group, level == 3 ? 2 : level);
            }

            return Pack(match, group, inShadow ? 0 : 1, extras);
        }

        // ------------------------------------------------------------------ finders (one page or frame at a time)

        public static Found<IWebElement>? FindBestTextInput(IWebDriver driver, string labelText, string[] attributeHints)
        {
            var candidates = new List<(int Rank, IWebElement Item)>();

            foreach (var input in CollectElementsIncludingShadow(driver, "input").Where(IsInteractable))
            {
                string type = input.GetAttribute("type") ?? "text";
                if (BlockedInputTypes.Contains(type)) continue;

                candidates.Add((ScoreField(driver, input, labelText, attributeHints), input));
            }

            return Pick(candidates);
        }

        public static Found<RadioMatch>? FindBestRadioOption(IWebDriver driver, string groupLabel, string optionText)
        {
            var candidates = new List<(int Rank, RadioMatch Item)>();

            foreach (var radio in CollectElementsIncludingShadow(driver, "input[type='radio'], [role='radio']"))
            {
                var meta = GetRadioMetadata(driver, radio);
                if (!IsInteractable(radio) && (meta.ClickableLabel == null || !IsInteractable(meta.ClickableLabel)))
                    continue;

                int rank = ScoreRadio(radio, meta.OptionTexts, meta.GroupTexts, meta.InShadowOrSlot, groupLabel, optionText);
                candidates.Add((rank, new RadioMatch { Radio = radio, Label = meta.ClickableLabel }));
            }

            return Pick(candidates);
        }

        public static Found<IWebElement>? FindBestDropdown(IWebDriver driver, string labelText)
        {
            string[] hints = { labelText.ToLowerInvariant() };

            var selects = new List<(int Rank, IWebElement Item)>();
            foreach (var select in CollectElementsIncludingShadow(driver, "select").Where(IsInteractable))
                selects.Add((ScoreField(driver, select, labelText, hints), select));

            var best = Pick(selects);
            if (best != null) return best;

            var triggers = new List<(int Rank, IWebElement Item)>();
            foreach (var trigger in CollectElementsIncludingShadow(driver, "[role='combobox'], [role='listbox'], .dropdown-toggle").Where(IsInteractable))
                triggers.Add((ScoreField(driver, trigger, labelText, hints), trigger));

            return Pick(triggers);
        }

        // ------------------------------------------------------------------ searching the page AND every frame

        // Looks in the page and in every iframe (nested, and inside shadow roots), keeps the best candidate overall,
        // then leaves the driver switched into the frame that contains it.
        public static T LocateBest<T>(IWebDriver driver, WebDriverWait wait, string fieldName,
            Func<Found<T>?> finder, Func<T, IWebElement> asElement) where T : class
        {
            DateTime? weakSince = null;

            try
            {
                return wait.Until(d =>
                {
                    var hits = new List<(int Rank, int RunnerUp, List<int> Path)>();
                    try
                    {
                        d.SwitchTo().DefaultContent();
                        ScanContexts(d, finder, new List<int>(), hits);
                    }
                    finally
                    {
                        d.SwitchTo().DefaultContent();
                    }

                    if (hits.Count == 0) return null;

                    // OrderByDescending is stable, so an exact tie goes to the earlier page/frame in DOM order.
                    var ranked = hits.OrderByDescending(h => h.Rank).ToList();
                    var top = ranked[0];

                    // Only weak evidence so far (no exact label): give the page a moment to finish rendering.
                    if (MatchOf(top.Rank) < 3)
                    {
                        weakSince ??= DateTime.UtcNow;
                        if (DateTime.UtcNow - weakSince.Value < SettleWindow) return null;
                    }

                    EnterFrame(d, top.Path);
                    var best = finder();
                    if (best == null) return null;

                    int bonus = (3 - Math.Min(top.Path.Count, 3)) * 1000;
                    int next = Math.Max(best.RunnerUp > 0 ? best.RunnerUp + bonus : 0, ranked.Count > 1 ? ranked[1].Rank : 0);
                    string ambiguous = next == top.Rank ? " AMBIGUOUS: took the first in DOM order" : "";
                    Log.Add($"{Describe(d, asElement(best.Item))} (rank {top.Rank}, next {next}){ambiguous}");

                    return best.Item;
                })!;
            }
            catch (WebDriverTimeoutException)
            {
                throw new NoSuchElementException($"No element matching '{fieldName}' found within {wait.Timeout.TotalSeconds:0}s.");
            }
        }

        static void ScanContexts<T>(IWebDriver driver, Func<Found<T>?> finder, List<int> path,
            List<(int Rank, int RunnerUp, List<int> Path)> hits) where T : class
        {
            var hit = finder();
            if (hit != null)
            {
                // Shallower frames win exact ties.
                hits.Add((hit.Rank + (3 - Math.Min(path.Count, 3)) * 1000, hit.RunnerUp, new List<int>(path)));
            }

            if (path.Count >= MaxFrameDepth) return;

            var frames = CollectElementsIncludingShadow(driver, "iframe");
            for (int i = 0; i < frames.Count; i++)
            {
                try { driver.SwitchTo().Frame(frames[i]); }
                catch (WebDriverException) { continue; }

                try { ScanContexts(driver, finder, new List<int>(path) { i }, hits); }
                catch (WebDriverException) { }

                try { EnterFrame(driver, path); }
                catch (WebDriverException) { driver.SwitchTo().DefaultContent(); }
            }
        }

        static void EnterFrame(IWebDriver driver, List<int> path)
        {
            driver.SwitchTo().DefaultContent();
            foreach (int index in path)
            {
                driver.SwitchTo().Frame(CollectElementsIncludingShadow(driver, "iframe")[index]);
            }
        }

        // ------------------------------------------------------------------ talking to the page

        public static IReadOnlyList<IWebElement> CollectElementsIncludingShadow(IWebDriver driver, string cssSelector)
        {
            var raw = ((IJavaScriptExecutor)driver).ExecuteScript(BrowserScripts.QuerySelectorAllWithShadow, cssSelector) as IReadOnlyCollection<object>;
            return raw?.OfType<IWebElement>().ToList() ?? new List<IWebElement>();
        }

        static string Describe(IWebDriver driver, IWebElement element) =>
            ((IJavaScriptExecutor)driver).ExecuteScript(BrowserScripts.DescribeElement, element)?.ToString() ?? "?";

        static (List<string> LabelTexts, bool InShadowOrSlot) GetFieldMetadata(IWebDriver driver, IWebElement element)
        {
            var res = ((IJavaScriptExecutor)driver).ExecuteScript(BrowserScripts.GetFieldMetadata, element) as IReadOnlyList<object>;
            var list = (res?[0] as IReadOnlyCollection<object>)?.Select(o => o?.ToString() ?? "").ToList() ?? new List<string>();
            bool inShadow = res != null && res.Count > 1 && res[1] is bool b && b;
            return (list, inShadow);
        }

        static (List<string> OptionTexts, List<string> GroupTexts, IWebElement? ClickableLabel, bool InShadowOrSlot) GetRadioMetadata(IWebDriver driver, IWebElement radio)
        {
            var res = ((IJavaScriptExecutor)driver).ExecuteScript(BrowserScripts.GetRadioMetadata, radio) as IReadOnlyList<object>;
            var optList = (res?[0] as IReadOnlyCollection<object>)?.Select(o => o?.ToString() ?? "").ToList() ?? new List<string>();
            var grpList = (res?[1] as IReadOnlyCollection<object>)?.Select(o => o?.ToString() ?? "").ToList() ?? new List<string>();
            var clickable = res != null && res.Count > 2 ? res[2] as IWebElement : null;
            bool inShadow = res != null && res.Count > 3 && res[3] is bool b && b;
            return (optList, grpList, clickable, inShadow);
        }

        public static bool IsInteractable(IWebElement el)
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
}