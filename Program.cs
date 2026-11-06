using System;
using System.Collections.Generic;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace CloudQATask1
{
    class Program
    {
        static void Main(string[] args)
        {
            // Allow local file paths via args, default to live site
            string targetUrl = args.Length > 0 ? args[0] : "https://app.cloudqa.io/home/AutomationPracticeForm";
            
            Console.WriteLine($"Starting automation on: {targetUrl}");

            var options = new ChromeOptions();
            
            using (IWebDriver driver = new ChromeDriver(options))
            {
                driver.Navigate().GoToUrl(targetUrl);

                // Wait up to 10s for elements to load
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                
                try 
                {
                    Console.WriteLine("Attempting to fill 'First Name'...");
                    IWebElement firstNameInput = SmartFindInput(driver, "First Name");
                    firstNameInput.SendKeys("Saanvi");
                    Console.WriteLine("Successfully filled First Name.");

                    Console.WriteLine("Attempting to select 'Gender'...");
                    IWebElement genderRadio = SmartFindRadio(driver, "Female");
                    genderRadio.Click();
                    Console.WriteLine("Successfully selected Gender.");

                    Console.WriteLine("Attempting to select 'State'...");
                    IWebElement stateDropdown = SmartFindDropdown(driver, "State");
                    SelectElement select = new SelectElement(stateDropdown);
                    
                    // Pick the first available option (more stable than hardcoding "NCR")
                    select.SelectByIndex(1);
                    Console.WriteLine("Successfully selected State.");

                    // Pause so we can see the result before Chrome closes
                    Thread.Sleep(3000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Automation failed: {ex.Message}");
                }
            }
        }

        // --- Smart Locators ---
        // Fallback strategies to survive UI changes (missing IDs, new wrappers, etc.)

        static IWebElement SmartFindInput(IWebDriver driver, string fieldName)
        {
            // 1. Check placeholders
            var byPlaceholder = driver.FindElements(By.XPath($"//input[contains(translate(@placeholder, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{fieldName.ToLower()}')]"));
            if (byPlaceholder.Count > 0) return byPlaceholder[0];

            // 2. Look for a matching label and grab its 'for' attribute
            var labels = driver.FindElements(By.XPath($"//label[contains(text(), '{fieldName}')]"));
            if (labels.Count > 0)
            {
                string inputId = labels[0].GetAttribute("for");
                if (!string.IsNullOrEmpty(inputId))
                {
                    var byLabelFor = driver.FindElements(By.Id(inputId));
                    if (byLabelFor.Count > 0) return byLabelFor[0];
                }
            }

            // 3. Fallback to the input physically next to the label
            var inputNearLabel = driver.FindElements(By.XPath($"//label[contains(text(), '{fieldName}')]//following::input[1]"));
            if (inputNearLabel.Count > 0) return inputNearLabel[0];

            // 4. Last resort: fuzzy match on name or id attributes
            string fuzzyName = fieldName.Replace(" ", "").ToLower();
            var fuzzyMatch = driver.FindElements(By.XPath($"//input[contains(translate(@name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{fuzzyName}') or contains(translate(@id, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{fuzzyName}')]"));
            if (fuzzyMatch.Count > 0) return fuzzyMatch[0];

            throw new NoSuchElementException($"Could not find a text input for: {fieldName}");
        }

        static IWebElement SmartFindRadio(IWebDriver driver, string optionText)
        {
            // 1. Check specific radio value
            var radioByValue = driver.FindElements(By.XPath($"//input[@type='radio' and @value='{optionText}']"));
            if (radioByValue.Count > 0) return radioByValue[0];

            // 2. Fallback to the radio button right before the text label
            var radioByLabel = driver.FindElements(By.XPath($"//label[contains(text(), '{optionText}')]//preceding-sibling::input[@type='radio']"));
            if (radioByLabel.Count > 0) return radioByLabel[0];

            throw new NoSuchElementException($"Could not find a radio option for: {optionText}");
        }

        static IWebElement SmartFindDropdown(IWebDriver driver, string fieldName)
        {
            // 1. Find by label's 'for' linkage
            var labels = driver.FindElements(By.XPath($"//label[contains(text(), '{fieldName}')]"));
            if (labels.Count > 0)
            {
                string selectId = labels[0].GetAttribute("for");
                if (!string.IsNullOrEmpty(selectId))
                {
                    var byLabelFor = driver.FindElements(By.Id(selectId));
                    if (byLabelFor.Count > 0) return byLabelFor[0];
                }
            }

            // 2. Fallback to the select element physically following the label
            var selectNearLabel = driver.FindElements(By.XPath($"//label[contains(text(), '{fieldName}')]//following::select[1]"));
            if (selectNearLabel.Count > 0) return selectNearLabel[0];

            throw new NoSuchElementException($"Could not find a dropdown for: {fieldName}");
        }
    }
}