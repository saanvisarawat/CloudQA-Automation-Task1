# CloudQA Task 1: Resilient Form Automation

A C# and Selenium program that fills in three fields on the CloudQA practice form: **First Name** (text box), **Gender** (radio button) and **State** (dropdown). It is built to keep working when the page changes: fields reordered, ids / names / classes renamed, extra wrappers added, or look-alike fields placed around the real ones.

It does not use a fixed XPath or CSS selector for any of the three fields. Instead it looks at every candidate on the page, scores how well each one matches, and picks the best.

## Setup

You need:
- .NET 8 SDK
- Google Chrome (Selenium Manager downloads the matching driver automatically)

```bash
git clone https://github.com/saanvisarawat/CloudQA-Automation-Task1.git
cd CloudQA-Automation-Task1
dotnet build src
```

## Running it

```bash
# The live CloudQA page (default)
dotnet run --project src

# Any other URL (this is how the page URL is configurable)
dotnet run --project src -- https://example.com/modified-form

# A local copy of the page
dotnet run --project src -- samples\v00_baseline.html

# Optional: choose the values to enter
dotnet run --project src -- <url> <first name> <gender> <state>
dotnet run --project src -- <url> Saanvi Female India
```

Set `HEADLESS=1` to hide the browser window.

The arguments, in order, are the page URL (or a local `.html` path), the first name, the gender option and the state. If the requested state is not in the list, the program picks the first real option and says so in the log.

### Reading the output

```
[PASS] First Name (Text Box) -> <input id=fname name=fname placeholder=First Name> (rank 3013020, next 0)
[PASS] Gender (Radio Button) -> <input name=Gender type=radio value=Female> (rank 3213010, next 0)
[PASS] State (Dropdown) -> <select id=state name=state> (rank 3013015, next 0)
Summary: 3 passed, 0 failed.
```

Each line names the element that was chosen. `rank` is its score and `next` is the score of the runner-up, so a small gap means the choice was close. If two candidates tie exactly, the line says `AMBIGUOUS`.

The exit code is `0` when all three tests pass and `1` otherwise, so the program can be used from a script or CI.

## How it works

1. **Collect candidates.** A small JavaScript snippet gathers every `input`, `select` or radio on the page, including ones inside open shadow DOM. Hidden and disabled elements are ignored.
2. **Read the evidence for each one.** The nearest label (via `for=`, a wrapping label, `aria-labelledby`, or the closest label-like element before it), plus its `name`, `aria-label`, `placeholder` and, for radios, the group label.
3. **Rank them in tiers.** The score is built so that a higher tier always beats a lower one, however much the lower tier adds up to:

   | Tier | What it means |
   |---|---|
   | Match | 3 = exact label or option text, 2 = exact `name` / `aria-label` / `placeholder` / `value`, 1 = partial match |
   | Group (radios) | 2 = the group label is exactly "Gender", 1 = it contains it |
   | Light DOM | normal page beats shadow DOM, but only as a tie-breaker |
   | Extras | small bonuses, for example `id=fname` |

   The reason for tiers: with plain added-up points, a look-alike with a lot of small matches could beat the real field with one strong match. I found exactly that in testing (a "Guardian Gender" group whose radio had `value="Female"` beat the real Gender group), so the design changed.
4. **Search every frame.** It looks in the page and in every iframe, including nested ones and iframes inside shadow DOM (up to 3 levels deep). It keeps the best candidate overall, not the first match, then switches into the right frame.
5. **Wait for the page.** If the best match is weak (no exact label), it keeps checking for up to 2 seconds in case the real form is still rendering. Overall each field waits up to 10 seconds.
6. **Check the result.** The text box value is read back. The radio must actually be checked. A native dropdown must show the chosen option. For a custom dropdown, only options that appear *after* the click are considered, and the trigger must show the chosen text afterwards. If it cannot be verified, the test fails instead of passing.

Common wording differences are handled with a small synonym list (for example "Given Name", "Forename", "Province", "Region", "Sex").

## Testing

`samples/` contains test pages I used to try the program on changed layouts. Run all of them with:

```powershell
powershell -ExecutionPolicy Bypass -File samples\RunSuite.ps1          # headless
powershell -ExecutionPolicy Bypass -File samples\RunSuite.ps1 -Visible # watch it work
```

The suite builds once, runs every page, and prints one PASS/FAIL row per page. The pages cover:

- reordered and renamed fields, and extra wrapper elements
- flat, table and nested-label layouts, and placeholder-only fields
- look-alike fields (for example "Preferred First Name", "State of Birth", a second gender group)
- hidden radios with styled labels, ARIA radios, and a custom dropdown with a navigation menu on the page
- the form inside an iframe, a nested iframe, an iframe inside shadow DOM, and the form inside shadow DOM
- a form that renders a few seconds after load, and a decoy that appears before the real field

Result at the time of writing: all pages in the suite pass. Live CloudQA page: **[fill in after running: pass / what it picked]**.

## Approach and limitations

What I decided on, and what I know it does not do:

- **Scoring instead of selectors.** This is more robust to page changes, but it is a heuristic. It chooses the *most likely* field, not a guaranteed one. That is why the log shows the runner-up score.
- **Label wording.** A label that is far from the field's name and not in the synonym list (for example "Region of residence" for State) will not be found.
- **Closed shadow DOM** cannot be read by any script, so those fields are not found.
- **Frame depth** is limited to 3 levels.
- **Custom widgets** are supported only in common forms: `role="radio"` radios, and dropdowns marked `role="combobox"`, `role="listbox"` or `.dropdown-toggle` whose options appear in the page after a click.
- **Ties** between two equally good candidates go to the one earlier in the page (and in the shallower frame). The log marks this as `AMBIGUOUS`.
- **Weak matches take longer.** If a field only matches by `placeholder` or `aria-label`, the program waits 2 seconds before accepting it.
- **Value fallback.** If the requested State is not offered, it picks the first real option rather than failing, and logs it. If you would prefer a hard failure here, that is a one-line change in `Program.cs`.
- **Tested on Chrome only.**

## AI use

## AI use

I designed the core architecture, the tiered ranking algorithm, and the test suite. To work efficiently, I leveraged Generative AI as a collaborative assistant—primarily as a sounding board to refine the scoring math and to help draft the JavaScript snippets needed for DOM metadata extraction. The critical engineering decisions, such as the global frame sweep, the custom-dropdown fallbacks, and the shift from additive scoring to rigid tiers, were driven by my own manual testing against edge cases. Because I drove the design and actively debugged the failures, I can confidently explain the technical rationale behind the code.

## Project layout

```
src/
  Program.cs          entry point, the three tests, result checks
  SmartLocator.cs     scoring, ranking, frame search
  BrowserScripts.cs   the JavaScript that runs inside the page
  CloudQATask1.csproj
samples/
  RunSuite.ps1        runs every sample page
  *.html              test pages
```