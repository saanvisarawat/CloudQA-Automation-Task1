
namespace CloudQATask1
{
    class Program
    {
        static int Main(string[] args)
        {
            string url = args.Length > 0 ? args[0] : "https:
            if (!url.StartsWith("http") && !url.StartsWith("file")) url = "https:

            var options = new ChromeOptions();
            if (Environment.GetEnvironmentVariable("HEADLESS") == "1") options.AddArgument("--headless=new");
            
            int exitCode = 0;
            using IWebDriver driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl(url);

            
            try {
                var nameMatch = SmartLocator.Find(driver, "First Name", "input", "firstname", "given name");
                nameMatch.ClickableTarget.Clear();
                nameMatch.ClickableTarget.SendKeys("Saanvi");
                if (nameMatch.Element.GetAttribute("value") != "Saanvi") throw new Exception("Value check failed.");
            } catch (Exception e) { Console.WriteLine($"❌ First Name Failed: {e.Message}"); exitCode = 1; }

            
            try {
                var genderMatch = SmartLocator.Find(driver, "Female", "radio", "gender");
                genderMatch.ClickableTarget.Click();
                if (!genderMatch.Element.Selected) throw new Exception("Radio not selected after click.");
            } catch (Exception e) { Console.WriteLine($"❌ Gender Failed: {e.Message}"); exitCode = 1; }

            
            try {
                var stateMatch = SmartLocator.Find(driver, "State", "dropdown", "region", "country");
                
                if (stateMatch.Element.TagName.ToLower() == "select") {
                    new SelectElement(stateMatch.Element).SelectByText("India");
                } else {
                    stateMatch.ClickableTarget.Click(); 
                    var option = new WebDriverWait(driver, TimeSpan.FromSeconds(3)).Until(d => 
                        d.FindElements(By.CssSelector("[role='option'], .dropdown-item")).FirstOrDefault(e => e.Text.Contains("India")));
                    if (option == null) throw new Exception("Menu opened but 'India' not found.");
                    option.Click();
                    
                    
                    if (!stateMatch.ClickableTarget.Text.Contains("India")) throw new Exception("Custom dropdown did not retain value.");
                }
            } catch (Exception e) { Console.WriteLine($"❌ State Failed: {e.Message}"); exitCode = 1; }

            return exitCode;
        }
    }
}