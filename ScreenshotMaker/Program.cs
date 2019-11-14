using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenshotMaker
{
    class Program
    {
        private static readonly ThreadLocal<ChromeDriver> Driver = new ThreadLocal<ChromeDriver>( true );

        private static List<string> _urlList;
        private static NameValueCollection _applicationSettings;

        private static void Main( string[] args )
        {
            //Get settings from .config file
            _applicationSettings = ConfigurationManager.GetSection( "ApplicationSettings" ) as NameValueCollection;

            //Parse the list of urls from the file
            _urlList = File.ReadAllLines( _applicationSettings?["DataSource"] ?? throw new InvalidOperationException() ).ToList();

            //Take screenshots according the urls list in parallel
            Parallel.ForEach( _urlList,
                new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt16( _applicationSettings["MaxThreadCount"] ) },
                GoToPageAndTakeScreenshot );

            CloseChromeDrivers();

            Console.WriteLine( "Work is done. Press any key to EXIT..." );
            Console.ReadKey();
        }

        private static void GoToPageAndTakeScreenshot( string url )
        {
            StartChromeDriver();

            if (string.IsNullOrWhiteSpace( url ))
                return;

            Console.WriteLine( $"------------ Opening \"{url}\" in thread {Task.CurrentId} ------------" );

            Driver.Value.Navigate().GoToUrl( url.Trim() );

            //Wait the page to be completely loaded
            new WebDriverWait( Driver.Value, TimeSpan.FromSeconds( 30 ) )
                .Until( wd => ((IJavaScriptExecutor)wd).ExecuteScript( "return document.readyState" ).ToString().Equals( "complete" ) );

            TakeFullPageScreenshot( Driver.Value, url );
        }

        private static ChromeOptions InitOptions()
        {
            var options = new ChromeOptions();
            options.AddArgument( "window-size=1920,1080" );
            options.AddArguments( "headless" );
            options.AddArgument( "--silent" );
            options.AddArgument( "--disable-gpu" );
            options.AddArgument( "--log-level=3" );

            return options;
        }

        private static void StartChromeDriver()
        {
            if (!Driver.IsValueCreated)
            {
                Driver.Value = new ChromeDriver( _applicationSettings["ChromedriverPath"], InitOptions() );
                Driver.Value.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds( 3 );
            }
        }

        private static void CloseChromeDrivers()
        {
            foreach (var driver in Driver.Values)
            {
                driver.Quit();
            }
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

            Console.WriteLine( $"------------ Screenshot for \"{url}\" has been taken in thread {Task.CurrentId} ------------" );

            //Save the screenshot
            screenshot.SaveAsFile( Path.Combine( CreateDirectory( _applicationSettings["DirectoryForScreenshots"] ), $"{name}.jpg" ) );

            Console.WriteLine( $"------------ Screenshot for \"{url}\" has been saved in thread {Task.CurrentId} ------------" );

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
