using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Database_Designer
{
    public partial class Roadmap : Page
    {
        public MainPage mainPage;

        bool IsSetup = false;
        public UIWindowEntry WindowInfo { get; private set; }

        public Roadmap(MainPage mainPaged)
        {
            this.InitializeComponent();
            RoadmapUISetter();
            mainPaged.IntroPage.Children.Remove(this);

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

        // Executes when the user navigates to this page.  
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RoadmapUISetter();
        }

        private void RoadmapUISetter()
        {
            if (IsSetup)
            {
                return;
            }

            var parentGrid = MainPanel;


            List<string> Phase1A = new()
            {
               "Database Designer Release!",
               "Username/Password Based Sessions",
               "Postgres Based SQL File Generation",
               "Markdown Based Documentation Generation",
               "API C# Based File Documentation"
            };

            List<string> Phase1B = new()
               {
                   "DMs, Chatrooms, Servers, Castles and Parties",
                   "Multiple Message Formats Supported",
                   "Centralized And Decentralized Formats",
                   "Username/Password Based Login With 2FA"
               };

            List<string> Phase2A = new()
               {
                   "SSO Login Added + Custom Data Types",
                   "Single and Multivendor Markwtplaces w Payments",
                   "Online Library For Remembering Digital Purchases (Centralized)",
                   "Mod System For Custom Templates"
               };

            List<string> Phase2B = new()
               {
                   "DMs, Chatrooms, Servers, Castles and Parties",
                   "WalkerWorks Released!",
                   "More System Plugins Like Ratings, Comments And User Media Sharing"
               };

            List<string> Phase3A = new()
            {
               "Recheck/Revamp Pariah Cybersecurity",
               "Visual Programmer For Database Designer API",
            };

            List<string> Phase3B = new()
               {
                   "Project Replicant Released!",
                   "Frontend Partial UI Generation",
                   "Automated SQL File Running",
                   "Local HTML Website Based Documentation",
                   "True Database Designing (Create ANY Custom Table/Type)"
               };

            List<string> Phase4A = new()
               {
                   "XR Based Designer UI",
                   "???"
               };

            var phaseA = new List<List<string>> { Phase1A, Phase1B };

            var phaseB = new List<List<string>> { Phase2A, Phase2B };

            var phaseC = new List<List<string>> { Phase3A, Phase3B };

            var phaseD = new List<List<string>> { Phase4A };


            CreatePhaseUI(phaseA, "1", new List<Color>
            {
                Color.FromArgb(0xFF, 0xA3, 0xB1, 0x63),
                Color.FromArgb(0xFF, 0x63, 0xB1, 0x78)
            }, parentGrid);

            CreatePhaseUI(phaseB, "2", new List<Color>
            {
                Color.FromArgb(0xFF, 0xB1, 0x63, 0x95),
                Color.FromArgb(0xFF, 0xB1, 0x96, 0x63)
            }, parentGrid);

            CreatePhaseUI(phaseC, "3", new List<Color>
            {
                Color.FromArgb(0xFF, 0x63, 0xB1, 0xA4),
                Color.FromArgb(0xFF, 0x63, 0x82, 0xB1)
            }, parentGrid);

            CreatePhaseUI(phaseD, "4", new List<Color>
            {
                Color.FromArgb(0xFF, 0xB1, 0x63, 0x63)
            }, parentGrid);


            IsSetup = true;


        }


        private void CreatePhaseUI(List<List<string>> phaseList, string phaseName, List<Color> colors, StackPanel parentGrid)
        {


            StackPanel phasePanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Width = 310,
                Height = 839,
                Background = new SolidColorBrush(Colors.Transparent),
                Margin = new Thickness(10)
            };

            parentGrid.Children.Add(phasePanel);

            Grid mainPhaseName = new Grid
            {
                Width = 310,
                Height = 50,
                Background = new SolidColorBrush(Color.FromArgb(255, 157, 151, 133)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Visible
            };

            phasePanel.Children.Add(mainPhaseName);

            TextBlock mainPhaseText = new TextBlock
            {
                Text = $"Phase {phaseName}",
                FontSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                Foreground = new SolidColorBrush(Colors.Black),

            };

            mainPhaseName.Children.Add(mainPhaseText);

            List<string> subphases = new List<string> { "A", "B", "C", "D", "E" };

            int subPhase = 0;

            foreach (var phaseListItem in phaseList)
            {
                string subphaseLabel = subPhase < subphases.Count ? subphases[subPhase] : $"Sub{subPhase + 1}";
                Color subPhaseColor = subPhase < colors.Count ? colors[subPhase] : Colors.Gray;

                var subPhaseGrid = new Grid
                {
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x9D, 0x97, 0x85)),
                    Margin = new Thickness(0, 5, 0, 0),
                    Visibility = Visibility.Visible
                };

                phasePanel.Children.Add(subPhaseGrid);

                var subPhaseText = new TextBlock
                {
                    Text = $"[{subphaseLabel}]",
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    Foreground = (Brush)Application.Current.Resources["Theme_BackgroundColor"]
                };

                subPhaseGrid.Children.Add(subPhaseText);

                foreach (string phaseDetail in phaseListItem)
                {
                    var roadmapItem = new Grid
                    {
                        Margin = new Thickness(0, 5, 0, 0),
                        Background = new SolidColorBrush(subPhaseColor),
                        Visibility = Visibility.Visible
                    };

                    var roadmapText = new TextBlock
                    {
                        Text = phaseDetail,
                        FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                        Width = 230,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 10, 0, 10),

                        Foreground = new SolidColorBrush(Colors.White)
                    };

                    roadmapItem.Children.Add(roadmapText);
                    phasePanel.Children.Add(roadmapItem);
                }

                subPhase++; 
            }
        }


    }
}
