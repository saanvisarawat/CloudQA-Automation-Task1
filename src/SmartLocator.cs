using System;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace CloudQATask1
{
    public class ScoredElement : IComparable<ScoredElement>
    {
        public IWebElement Element { get; set; }
        public IWebElement ClickableTarget { get; set; }
        public int FrameIndex { get; set; } = -1; 
        
        
        public int Score { get; set; }
        public string DiagnosticLog { get; set; }

        public int CompareTo(ScoredElement other) => other.Score.CompareTo(this.Score); 
    }

    public static class SmartLocator
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public static ScoredElement Find(IWebDriver driver, string targetLabel, string elementType, params string[] synonyms)
        {
            WebDriverWait wait = new WebDriverWait(driver, Timeout);
            
            try
            {
                return wait.Until(d =>
                {
                    List<ScoredElement> allCandidates = new List<ScoredElement>();

                    
                    d.SwitchTo().DefaultContent();
                    allCandidates.AddRange(SweepContext(d, -1, targetLabel, elementType, synonyms));

                    
                    var frames = d.FindElements(By.TagName("iframe"));
                    for (int i = 0; i < frames.Count; i++)
                    {
                        try
                        {
                            d.SwitchTo().DefaultContent();
                            d.SwitchTo().Frame(i);
                            allCandidates.AddRange(SweepContext(d, i, targetLabel, elementType, synonyms));
                        }
                        catch (WebDriverException) {  }
                    }

                    d.SwitchTo().DefaultContent();

                    if (!allCandidates.Any()) return null;

                    allCandidates.Sort();
                    var best = allCandidates.First();
                    var runnerUp = allCandidates.Count > 1 ? allCandidates[1] : null;

                    
                    if (best.Score < 10) return null; 

                    LogDecision(targetLabel, best, runnerUp);

                    
                    if (best.FrameIndex != -1) d.SwitchTo().Frame(best.FrameIndex);

                    return best;
                });
            }
            catch (WebDriverTimeoutException)
            {
                throw new Exception($"Timeout: Could not locate a confident match for '{targetLabel}' after 10s.");
            }
        }

        private static List<ScoredElement> SweepContext(IWebDriver context, int frameIndex, string targetLabel, string elementType, string[] synonyms)
        {
            var results = new List<ScoredElement>();
            var targets = new List<string> { targetLabel.ToLower() };
            targets.AddRange(synonyms.Select(s => s.ToLower()));

            string css = elementType == "input" ? "input:not([type='hidden']):not([type='radio']):not([type='checkbox'])" :
                         elementType == "radio" ? "input[type='radio']" : 
                         "select, [role='combobox'], .dropdown-toggle";

            var candidates = (IReadOnlyCollection<IWebElement>)((IJavaScriptExecutor)context)
                .ExecuteScript(BrowserScripts.QuerySelectorAllWithShadow, css);

            foreach (var el in candidates.Where(e => e.Displayed && e.Enabled))
            {
                var meta = (Dictionary<string, object>)((IJavaScriptExecutor)context)
                    .ExecuteScript(BrowserScripts.GetElementMetadata, el);

                int score = CalculateTieredScore(meta, targets, elementType);
                if (score > 0)
                {
                    results.Add(new ScoredElement 
                    { 
                        Element = el, 
                        ClickableTarget = DetermineClickTarget(el, meta, elementType),
                        FrameIndex = frameIndex, 
                        Score = score,
                        DiagnosticLog = $"id='{meta["id"]}', type='{elementType}'"
                    });
                }
            }
            return results;
        }

        private static int CalculateTieredScore(Dictionary<string, object> meta, List<string> targets, string elementType)
        {
            int score = 0;
            string rawLabels = meta["labels"].ToString().ToLower();
            string rawGroup = meta["group"].ToString().ToLower();
            string rawAttrs = meta["attributes"].ToString().ToLower();
            bool isShadow = (bool)meta["isShadow"];

            
            if (targets.Any(t => rawLabels.Contains($"\\b{t}\\b"))) score += 10000;
            else if (targets.Any(t => rawLabels.Contains(t))) score += 5000;

            
            if (elementType == "radio")
            {
                if (targets.Any(t => rawGroup.Contains($"\\b{t}\\b"))) score += 1000;
                else if (targets.Any(t => rawGroup.Contains(t))) score += 500;
            }

            
            if (targets.Any(t => rawAttrs.Contains($"\\b{t}\\b"))) score += 100;
            else if (targets.Any(t => rawAttrs.Contains(t))) score += 50;

            
            if (isShadow) score -= 1;

            return score;
        }

        private static IWebElement DetermineClickTarget(IWebElement el, Dictionary<string, object> meta, string elementType)
        {
            return elementType == "radio" && meta.ContainsKey("associatedLabel") && meta["associatedLabel"] != null
                ? (IWebElement)meta["associatedLabel"]
                : el;
        }

        private static void LogDecision(string targetName, ScoredElement best, ScoredElement runnerUp)
        {
            Console.WriteLine($"✔ Analyzed '{targetName}' -> Selected {best.DiagnosticLog} (Score: {best.Score})");
            if (runnerUp != null && (best.Score - runnerUp.Score < 100))
            {
                Console.WriteLine($"   [!] Warning: Narrow margin. Runner-up score: {runnerUp.Score} ({runnerUp.DiagnosticLog})");
            }
        }
    }
}