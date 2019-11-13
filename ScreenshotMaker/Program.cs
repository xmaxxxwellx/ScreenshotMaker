using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScreenshotMaker
{
    class Program
    {
        private const string ChromedriverPath = "Chromedriver";
        private const string FileWithUrlsList = "PagesToAnalyse.txt";
        private const string DirectoryForScreenshots = "Screenshots";

        static void Main( string[] args )
        {
            //Parse the list of urls from the file
            var urlsList = File.ReadAllLines( FileWithUrlsList ).ToList();

            //Create an instance of webdriver with needed options
            var options = new ChromeOptions();
            options.AddArgument( "window-size=1920,1080" );
            options.AddArguments( "headless" );
            options.AddArgument( "--silent" );
            options.AddArgument( "--disable-gpu" );
            options.AddArgument( "--log-level=3" );

            var driver = new ChromeDriver( ChromedriverPath, options );
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds( 3 );

            foreach (var url in urlsList)
            {
                if (!string.IsNullOrWhiteSpace( url ))
                {
                    Console.WriteLine( $"------------ Opening \"{url}\" ------------" );

                    driver.Navigate().GoToUrl( url.Trim() );

                    //Wait the page to be completely loaded
                    new WebDriverWait( driver, TimeSpan.FromSeconds( 30 ) )
                        .Until( wd => ((IJavaScriptExecutor)wd).ExecuteScript( "return document.readyState" ).ToString().Equals( "complete" ) );

                    TakeFullPageScreenshot( driver, url );
                }
            }

            driver.Close();
            driver.Quit();

            Console.WriteLine( "Work is done. Press any key to EXIT..." );
            Console.ReadKey();
        }

        private static void TakeFullPageScreenshot( ChromeDriver driver, string url )
        {
            var name = new Uri( url ).AbsolutePath.Replace( '/', '_' );

            //Dictionary will contain the parameters needed to get the full page screen shot
            var metrics = new Dictionary<string, object>
            {
                ["width"] = driver
                    .ExecuteScript( "return Math.max(window.innerWidth,document.body.scrollWidth,document.documentElement.scrollWidth)" ),
                ["height"] = driver
                    .ExecuteScript( "return Math.max(window.innerHeight,document.body.scrollHeight,document.documentElement.scrollHeight)" ),
                ["deviceScaleFactor"] = driver
                    .ExecuteScript( "return window.devicePixelRatio" ),
                ["mobile"] = driver
                    .ExecuteScript( "return typeof window.orientation !== 'undefined'" )
            };

            //Execute the emulation Chrome Command to change browser to a custom device that is the size of the entire page
            driver.ExecuteChromeCommand( "Emulation.setDeviceMetricsOverride", metrics );

            //Then just take screenshot as driver thinks that everything is visible
            var screenshot = driver.GetScreenshot();

            Console.WriteLine( $"------------ Screenshot for \"{url}\" has been taken ------------" );

            //Save the screenshot
            screenshot.SaveAsFile( Path.Combine( CreateDirectory( DirectoryForScreenshots ), $"{name}.jpg" ) );

            Console.WriteLine( $"------------ Screenshot for \"{url}\" has been saved ------------" );

            //This command will return browser back to a normal, usable form if need to do anything else with it.
            driver.ExecuteChromeCommand( "Emulation.clearDeviceMetricsOverride", new Dictionary<string, object>() );
        }

        private static string CreateDirectory( string path )
        {
            if (!Directory.Exists( path ))
            {
                Directory.CreateDirectory( path );
            }

            return path;
        }
    }
}
