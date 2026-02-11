using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Browser;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Xml.Linq;

namespace Database_Designer
{
    public partial class DatabaseDesigner : Page
    {
        private MainPage mainPaged; // Add a reference to MainPage  
        public UIWindowEntry WindowInfo { get; private set; }

        public DatabaseDesigner(MainPage mainPage)
        {
            InitializeComponent();
            this.mainPaged = mainPage;

            AboutRoadmapButton.Click += (s, Effect) =>
            {


                mainPage.CreateWindow(() => new Roadmap(mainPage), "Roadmap", true);
            };

            AboutFAQButton.Click += (s, Effect) =>
            {
                var uuid = Pariah_Cybersecurity.Utilities.CreateUUID();


                mainPage.CreateWindow(() => new FAQ(mainPage), "FAQ", true);
            };

            CreditsButton.Click += (s, Effect) =>
            {
                Main.Visibility = Visibility.Collapsed;
                Credits.Visibility = Visibility.Visible;
            };

            Back.Click += (s, Effect) =>
            {
                Main.Visibility = Visibility.Visible;
                Credits.Visibility = Visibility.Collapsed;
            };

            AboutWebsite.Click += (s, e) =>
            {
                HtmlPage.Window.Navigate(new Uri("https://walkerindustries.xyz/"), "_blank");
            };

            mainPage.IntroPage.Children.Remove(this);


        

                                ExitButton.Click += (s, e) => { try { if (mainPage.IntroPage.Children.Contains(this)) mainPage.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };
            

            this.Unloaded += (s, e) =>
            {
                RemoveWindow();
            };



        }


        public void RemoveWindow()
        {
            try
            {
                //if (WindowInfo == null) return;

                if (mainPaged?.LowerAppBar != null && WindowInfo.Shortcut != null)
                {
                    try
                    {
                        if (mainPaged.LowerAppBar.Children.Contains(WindowInfo.Shortcut))
                            mainPaged.LowerAppBar.Children.Remove(WindowInfo.Shortcut);
                        if (WindowInfo.Shortcut is FrameworkElement sh) sh.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }

                if (WindowInfo.Elements != null && mainPaged?.IntroPage != null)
                {
                    for (int i = WindowInfo.Elements.Count - 1; i >= 0; i--)
                    {
                        var item = WindowInfo.Elements[i];
                        try
                        {
                            if (item is FrameworkElement fe)
                            {
                                fe.Visibility = Visibility.Collapsed;
                                fe.DataContext = null;
                            }
                            if (mainPaged.IntroPage.Children.Contains(item))
                                mainPaged.IntroPage.Children.Remove(item);
                        }
                        catch { }
                    }
                    try { WindowInfo.Elements.Clear(); } catch { }
                }

                try
                {
                    if (this is FrameworkElement me) me.Visibility = Visibility.Collapsed;
                    if (mainPaged?.IntroPage != null && mainPaged.IntroPage.Children.Contains(this))
                        mainPaged.IntroPage.Children.Remove(this);
                }
                catch { }
            }
            catch { }

        }


        // Executes when the user navigates to this page.  
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }
    }
}
