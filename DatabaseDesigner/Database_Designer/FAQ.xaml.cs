using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Database_Designer
{
    public partial class FAQ : Page
    {
        public UIWindowEntry WindowInfo { get; private set; }

        MainPage mainPage;
        public FAQ(MainPage mainPaged)
        {
            this.InitializeComponent();
            SetupFAQ();

            mainPage = mainPaged;


			ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };


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

                if (mainPage?.LowerAppBar != null && WindowInfo.Shortcut != null)
                {
                    try
                    {
                        if (mainPage.LowerAppBar.Children.Contains(WindowInfo.Shortcut))
                            mainPage.LowerAppBar.Children.Remove(WindowInfo.Shortcut);
                        if (WindowInfo.Shortcut is FrameworkElement sh) sh.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }

                if (WindowInfo.Elements != null && mainPage?.IntroPage != null)
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
                            if (mainPage.IntroPage.Children.Contains(item))
                                mainPage.IntroPage.Children.Remove(item);
                        }
                        catch { }
                    }
                    try { WindowInfo.Elements.Clear(); } catch { }
                }

                try
                {
                    if (this is FrameworkElement me) me.Visibility = Visibility.Collapsed;
                    if (mainPage?.IntroPage != null && mainPage.IntroPage.Children.Contains(this))
                        mainPage.IntroPage.Children.Remove(this);
                }
                catch { }
            }
            catch { }

        }
        

        static string LicenseNotice =
@"Nope, — the generated files are free and open-source (FOSS), and you may use, modify, and redistribute them without restriction. However:

1. Service Restriction:
   You may not host, sell, or provide this program (in whole or part) as a service, whether commercial or noncommercial, without explicit written permission from the original author. This includes APIs, web applications, or cloud-based deployments.

2. Attribution and Fair Use:
   You may not claim exclusive ownership over specific outputs or attempt to license them in a way that prevents others from using similar content. You must also clearly reference the original source of the project: the Database Designer repository at https://github.com/Walker-Industries-RnD/Database-Designer, whenever using, modifying, or redistributing the work.

3. Malicious-Affiliation Ban:
   Use, modification, redistribution, or integration of this work (including derivatives) is prohibited by any individual or organization affiliated with former employees, contractors, or associates of Walker Industries (WI) who have:
   (a) breached or violated contractual or nondisclosure obligations, or
   (b) expressed intent to compromise WI's intellectual property or interests, whether or not an actual breach occurred. 
Learn more at the link below!

Violations of these terms will be treated as license violations and may result in enforcement.
";


        Dictionary<string, string> FaQInfo = new Dictionary<string, string>
        {
            { "Will This Ever Cost Money To Use Commercially?", "Nope! This is (and will always be) a completely open source program! You can customize it to your needs and do whatever you want with the result! The only thing I require is a mention to the repo; I also do not allow copyrighting/patenting as I believe it limits developers (Case in point: Nemesis System)." },
            { "Why Base This All Off C#?", "This was originally created as an internal tool for my (WalkerDev) projects! I love C# and use it almost exclusively, so it came naturally. If you don't want to use C# however, you could technically turn the output file into a DLL, then invoke the functions in the language of your choosing (Yes, I myself didn't know this was a thing for a while)! No other language is currently planned." },
            { "How Do I Use The Generated Files?", "The outputted SQL file should be ran through within Postgres, whereas you can use the C# within your project (Be it DLL, copy/pasted, etc). The Markdown is converted into Markdown for documentation!" },
            { "Are The Generated Files Under Any License??", LicenseNotice },
            { "Where Do I Go To Report Bugs or Request features?", "You can go to the Walker Industries server or the Database Designer Github!" },
            { "How Long Is The Time Between Each Phase?", "It honestly depends; all of these stages are made to both bring value to users while (first and foremost) bringing value to WI. As projects progress, so will DD." },
            { "Where Can I Talk To Others Using Database Designer?", "Join the Discord!" },
            { "What Is Mira’s Favorite Drink?", "Iced Coffee with a hint of Dr Pepper" },
            { "Does This Only Support Centralized/Decentralized?", "It depends! For example, some backend templates have it so making something centralized is pretty easy, while others are a flat out no. They're on a case by case basis!" },
            { "I Don’t Like The Generated API", "You're free to create your own, or recycle different parts that you do like from the templates provided!" },
            { "Is The API And Documentation Generated With An LLM (NO)", "Nope, this was all EXCRUCIATINGLY created with nothing but rulesets and modular code!" }
        };



        Dictionary<string, string> QuickLinks = new Dictionary<string, string>
        {
            {"WI Discord", "https://discord.gg/H8h8scsxtH"},
            {"Database Designer Github", "https://github.com/Walker-Industries-RnD/Database-Designer" },
            {"Malicious Affiliation Ban", "https://github.com/Walker-Industries-RnD/Malicious-Affiliation-Ban/tree/main"}
        };

   // 6
   // 4
   // 4



        List<StackPanel> FAQGrids = new List<StackPanel>();

        private void SetupFAQ()
        {
            var parent = FaQStack;

            var title = Title;
            var description = Description;

            int i = 0;

            foreach (var item in FaQInfo)
            {
                // Clone the title and description
                var clonedTitle = new Button
                {
                    Name = "Title" + i,
                    Content = item.Key,
                    Width = title.Width,
                    Height = title.Height,
                    Margin = title.Margin,
                    FontSize = 20,
                    FontFamily = title.FontFamily,
                    Background = title.Background,
                    Padding = title.Padding,
                    HorizontalAlignment = title.HorizontalAlignment,
                    VerticalAlignment = title.VerticalAlignment,
                    Visibility = Visibility.Visible
                };

                int localIndex = i; //Local copy

                clonedTitle.Click += (s, e) =>
                {
                    HandleFAQClick(localIndex);
                };

                var clonedDescription = new StackPanel
                {
                    Name = "Description" + i,
                    Width = description.Width,
                    Height = description.Height,
                    Margin = description.Margin,
                    Background = description.Background,
                    HorizontalAlignment = description.HorizontalAlignment,
                    VerticalAlignment = description.VerticalAlignment,
                    Visibility = Visibility.Collapsed
                };

                void CreateHyperLink(string Value)
                {
                    var hLink = new HyperlinkButton
                    {
                        Content = Value,
                        NavigateUri = new Uri(QuickLinks[Value]),
                        TargetName = "_blank",
                        Foreground = new SolidColorBrush(Colors.LightBlue),
                        Margin = new Thickness(5, 0, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Cursor = Cursors.Hand,
                        FontSize = 14
                    };

                    clonedDescription.Children.Add(hLink);
                }

                if (i == 1)
                {
                    CreateHyperLink("Database Designer Github");
                }

                if (i == 4)
                {
                    CreateHyperLink("Database Designer Github");
                    CreateHyperLink("Malicious Affiliation Ban");
                }

                if (i == 6)
                {
                    CreateHyperLink("WI Discord");
                }

                FAQGrids.Add(clonedDescription);





                var textBlock = new TextBlock
                {
                    Text = item.Value,
                    FontSize = 18,
                    Padding = new Thickness(14, 14, 14, 14),
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(10)
                };

                clonedDescription.Children.Add(textBlock);

                // Add clonedTitle to FaQStack
                parent.Children.Add(clonedTitle);
                // Add clonedDescription to FaQStack
                parent.Children.Add(clonedDescription);

                // Increment i
                i++;
            }



        }


        private void HandleFAQClick(int i)
        {
            foreach (StackPanel item in FAQGrids)
            {
                if (item.Name == "Description" + i)
                {
                    item.Visibility = item.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    item.Visibility = Visibility.Collapsed;
                }
            }
        }


    }
}
