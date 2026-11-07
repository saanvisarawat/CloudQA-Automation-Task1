using System;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace CloudQATask1
{
    class Program
    {
        // Usage: dotnet run -- [url or local .html path] [first name] [gender] [state]
        // Set HEADLESS=1 to hide the browser window.
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
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));

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

        // Accepts a full URL, a file:// URL, a path to a local .html file, or a bare host name.
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
            Log.Last = null;
            try
            {
                testAction();
                Console.WriteLine(Log.Last == null ? $"[PASS] {testName}" : $"[PASS] {testName} -> {Log.Last}");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FAIL] {testName}: {ex.Message}");
                return false;
            }
        }

        // ------------------------------------------------------------------ the three tests

        static void TestFirstName(IWebDriver driver, WebDriverWait wait, string expectedValue)
        {
            IWebElement input = SmartLocator.LocateBest(driver, wait, "First Name",
                () => SmartLocator.FindBestTextInput(driver, "First Name", new[] { "fname", "firstname", "given-name" }),
                e => e);

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
            RadioMatch match = SmartLocator.LocateBest(driver, wait, "Gender / " + optionLabel,
                () => SmartLocator.FindBestRadioOption(driver, "Gender", optionLabel),
                m => m.Radio);

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

            if (!IsChecked(radioInput) && clickableLabel != null)
            {
                try { clickableLabel.Click(); } catch (WebDriverException) { }
            }

            if (!IsChecked(radioInput))
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", radioInput);
            }

            if (!IsChecked(radioInput))
            {
                throw new Exception($"Radio option '{optionLabel}' was clicked, but it is still not checked.");
            }
        }

        static void TestState(IWebDriver driver, WebDriverWait wait, string preferredOption)
        {
            IWebElement dropdown = SmartLocator.LocateBest(driver, wait, "State",
                () => SmartLocator.FindBestDropdown(driver, "State"),
                e => e);

            ScrollIntoView(driver, dropdown);

            if (string.Equals(dropdown.TagName, "select", StringComparison.OrdinalIgnoreCase))
            {
                var select = new SelectElement(dropdown);

                IWebElement? exactOption = select.Options.FirstOrDefault(o =>
                    IsUsableOption(o) &&
                    (string.Equals(o.Text.Trim(), preferredOption, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(o.GetAttribute("value")?.Trim(), preferredOption, StringComparison.OrdinalIgnoreCase)));

                IWebElement? targetOption = exactOption ?? select.Options.FirstOrDefault(IsUsableOption);
                if (targetOption == null)
                {
                    throw new Exception("State <select> contained no selectable options.");
                }

                string expectedText = targetOption.Text.Trim();
                if (exactOption == null)
                {
                    Log.Add($"'{preferredOption}' is not offered, used '{expectedText}'");
                }

                select.SelectByText(targetOption.Text);

                string actualText = select.SelectedOption.Text.Trim();
                if (!string.Equals(actualText, expectedText, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Expected '{expectedText}', but selected option is '{actualText}'.");
                }
            }
            else
            {
                // Remember which options were already visible, so only options the click reveals are considered.
                const string optionSelector = "[role='option'], ul.dropdown-menu li, .dropdown-item, li";
                var visibleBefore = SmartLocator.CollectElementsIncludingShadow(driver, optionSelector)
                    .Where(SmartLocator.IsInteractable)
                    .ToHashSet();

                dropdown.Click();

                IWebElement? option;
                try
                {
                    option = wait.Until(d =>
                    {
                        var revealed = SmartLocator.CollectElementsIncludingShadow(d, optionSelector)
                            .Where(SmartLocator.IsInteractable)
                            .Where(i => !visibleBefore.Contains(i))
                            .ToList();

                        return revealed.FirstOrDefault(i => string.Equals(i.Text.Trim(), preferredOption, StringComparison.OrdinalIgnoreCase))
                            ?? revealed.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Text) && !IsPlaceholderText(i.Text));
                    });
                }
                catch (WebDriverTimeoutException)
                {
                    throw new Exception("Custom State dropdown was clicked, but no new options appeared.");
                }

                string chosenText = option!.Text.Trim();
                if (!string.Equals(chosenText, preferredOption, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Add($"'{preferredOption}' is not offered, used '{chosenText}'");
                }

                option.Click();

                if (!SelectionIsShown(dropdown, option, chosenText))
                {
                    throw new Exception($"Clicked '{chosenText}', but the dropdown does not show it as selected.");
                }
            }
        }

        // ------------------------------------------------------------------ small helpers

        static bool SelectionIsShown(IWebElement trigger, IWebElement option, string chosenText)
        {
            string?[] shownCandidates = { SafeText(trigger), trigger.GetAttribute("value"), trigger.GetAttribute("aria-label") };
            foreach (string? shown in shownCandidates)
            {
                if (!string.IsNullOrWhiteSpace(shown) && shown.Contains(chosenText, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            try
            {
                return string.Equals(option.GetAttribute("aria-selected"), "true", StringComparison.OrdinalIgnoreCase);
            }
            catch (WebDriverException)
            {
                return false;
            }
        }

        static string? SafeText(IWebElement element)
        {
            try { return element.Text; }
            catch (WebDriverException) { return null; }
        }

        // Real <input type=radio> uses Selected; ARIA radios (div role=radio) use aria-checked.
        static bool IsChecked(IWebElement element)
        {
            if (string.Equals(element.TagName, "input", StringComparison.OrdinalIgnoreCase))
                return element.Selected;

            return string.Equals(element.GetAttribute("aria-checked"), "true", StringComparison.OrdinalIgnoreCase);
        }

        static void ScrollIntoView(IWebDriver driver, IWebElement element)
        {
            try
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", element);
            }
            catch (WebDriverException) { }
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
    }
}