# CloudQA Task 1 – Heuristic Form Locator

A C#/Selenium testing script that dynamically locates and interacts with form fields without relying on fixed XPath or CSS selectors.

## Approach: Tiered DOM Scoring
Instead of brittle pathing, the `SmartLocator` engine extracts metadata (labels, group legends, attributes) via JavaScript and ranks candidates globally across the main document and all accessible iframes.

* **Tier 1 (10,000 pts):** Exact or partial match on associated `<label>`, `for` attribute, or adjacent text.
* **Tier 2 (1,000 pts):** Match on wrapping `<fieldset>` or group legend (critical for isolating radio groups).
* **Tier 3 (100 pts):** Match on attributes (`id`, `name`, `placeholder`, `aria-label`).
* **Tie-Breaker (-1 pt):** Elements inside Shadow DOM are penalized slightly so Light DOM equivalents win absolute ties.

This guarantees that a decoy field like `<input placeholder="Spouse First Name">` (Tier 3) mathematically cannot outscore the real `<label>First Name</label>` (Tier 1).

## Usage
```bash
# Run against the default live form
dotnet run

# Run against a modified local file with custom inputs
dotnet run "file: