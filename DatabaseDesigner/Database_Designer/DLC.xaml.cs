using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Browser;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using static Database_Designer.MiraMiniPopup;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;

namespace Database_Designer
{
    public partial class DLC : Page
    {
        private readonly MainPage mainPage;
        public UIWindowEntry WindowInfo { get; private set; }

        private readonly List<(string Text, VNStates Expression)> dialogs = new();
        private int currentDialogIndex = 0;


        //I have to actually make dialog sigh



        public DLC(MainPage mainPaged)
        {
            this.InitializeComponent();

            //Select a random preset
            SetupMasukiMiraConversation1();





            mainPage = mainPaged ?? throw new ArgumentNullException(nameof(mainPaged));

            Continue.Click += MiraMiniButton_Click;
            ExitButton.Click += (s, e) => ClosePopup();
            Unloaded += (s, e) => Cleanup();

            LoadList();

        }


        // Executes when the user navigates to this page.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }


        #region Visual Novel

        private void SetDialogs(List<(string Text, VNStates Expression)> textEntries)
        {
            dialogs.Clear();

            if (textEntries == null || textEntries.Count == 0)
            {
                dialogs.Add(("...", VNStates.MiraNeutral));
                UpdateDisplay();
                return;
            }

            dialogs.AddRange(textEntries);
            currentDialogIndex = 0;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (dialogs.Count == 0) return;

            VNText.Text = dialogs[currentDialogIndex].Text;

            // ← This is the important part: change image for current line
            SetVNImage(dialogs[currentDialogIndex].Expression);

            Continue.Content = (currentDialogIndex == dialogs.Count - 1)
                ? "That's All!"
                : "Next";
        }

        private void MiraMiniButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentDialogIndex < dialogs.Count - 1)
            {
                currentDialogIndex++;
                UpdateDisplay();
            }
            else
            {
            }
        }

        private void ClosePopup()
        {
            Visibility = Visibility.Collapsed;

            if (Parent is Panel parentPanel)
            {
                parentPanel.Children.Remove(this);
            }
            else if (mainPage?.IntroPage?.Children.Contains(this) == true)
            {
                mainPage.IntroPage.Children.Remove(this);
            }
            else if (mainPage?.DesktopCanvas?.Children.Contains(this) == true)
            {
                mainPage.DesktopCanvas.Children.Remove(this);
            }

            Cleanup();
        }

        private void Cleanup()
        {
            try
            {
                if (mainPage?.LowerAppBar != null)
                {
                    if (mainPage.LowerAppBar.Children.Contains(WindowInfo.Shortcut))
                        mainPage.LowerAppBar.Children.Remove(WindowInfo.Shortcut);
                    if (WindowInfo.Shortcut is UIElement el)
                        el.Visibility = Visibility.Collapsed;
                }

                if (mainPage?.IntroPage != null)
                {
                    foreach (var item in WindowInfo.Elements)
                    {
                        if (item is FrameworkElement fe)
                        {
                            fe.Visibility = Visibility.Collapsed;
                            fe.DataContext = null;
                        }
                        if (mainPage.IntroPage.Children.Contains(item))
                            mainPage.IntroPage.Children.Remove(item);
                    }
                    WindowInfo.Elements.Clear();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup failed: {ex.Message}");
            }
        }

        private void SetVNImage(VNStates state)
        {
            string fileName = state switch
            {
                VNStates.MiraNeutral => "MiraVN/MiraNeutral",
                VNStates.MasukiHappy => "Masuki/Happy",
                VNStates.MasukiNerd => "Masuki/Nerd",
                VNStates.MasukiDizzy => "Masuki/Dizzy",
                VNStates.MasukiSleep => "Masuki/Sleep",
                VNStates.MasukiStudious => "Masuki/Studious",
            };

            try
            {
                VNImg.Source = new BitmapImage(new Uri($"/Database_Designer;component/assets/images/{fileName}.png", UriKind.Relative));

            }
            catch
            {
                VNImg.Source = null;
            }
        }

        public enum VNStates
        {
            MiraNeutral,
            MasukiHappy,
            MasukiNerd,
            MasukiDizzy,
            MasukiSleep,
            MasukiStudious,
        }

        #endregion
        

        //DLC checks

        private async void LoadList()
        {
            DLCList.Children.Clear();

            var mod1 = GenerateDLCHeader("DB Designer's Post Production VN", "Header");
            DLCList.Children.Add(mod1);


        }

        Dictionary<string, string> DLCInfo = new Dictionary<string, string>
{
    {
        "DB Designer's Post Production VN",
        @"Meet the minds behind Database Designer: In this supporter's pack, get an exclusive Visual Novel-style interview explaining the thought processes and development behind the free software! 

It’s a fully styled Visual Novel experience that drops you into the actual development process of Database Designer.

This includes decisions, arguments, design philosophy, and the moments where things almost went completely off the rails."
    }
};


        private Grid GenerateDLCHeader(string ModType, string imgName)
        {
            Grid containerGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00)),
                Height = 355
            };

            Button themeButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00))
            };


            Image headerImage = new Image
            {
                Source = new BitmapImage(new Uri($"/Database_Designer;component/assets/images/dlc/{imgName}.png", UriKind.Relative)),
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 768,
                Height = 360,
                Opacity = 0.8,
                IsHitTestVisible = false
            };

            containerGrid.Children.Add(themeButton);
            containerGrid.Children.Add(headerImage); 

            themeButton.Click += (s, e) => NavigateToDetails();

            void NavigateToDetails()
            {


                DetailedDLC.Visibility = Visibility.Visible;
                InitialD.Visibility = Visibility.Collapsed;

                Title.Text = ModType;
                HeaderImg.Source = headerImage.Source;
                DLCInfoText.Text = DLCInfo[ModType];



                
                if (ModType == "DB Designer's Post Production VN")
                {
                    var dlcExists = IsDatabaseDesignerVNInstalled();

                    if (dlcExists)
                    {
                        LaunchBtn.Click += (s,e) => LaunchVN();
                    }
                    else
                    {

                        LaunchBtn.Click += (s, e) =>
                        { LaunchBtn.Content = "Not Installed"; };
                    }

                }

                VIsitStore.Click += (s, e) => Open("https://store.steampowered.com/app/4262740/Database_Designer__Supporter_Pack/");

                BackBtn.Click += (s, e) =>
                {
                    //Unattach all click logic
                    LaunchBtn.Click -= (s, e) => LaunchVN();
                    VIsitStore.Click -= (s, e) => Open("https://store.steampowered.com/app/4262740/Database_Designer__Supporter_Pack/");
                    LaunchBtn.Content = "Launch";

                    DetailedDLC.Visibility = Visibility.Collapsed;
                    InitialD.Visibility = Visibility.Visible;

                };


            }


            void Open(string url)
            {
                HtmlPage.Window.Navigate(new Uri(url), "_blank");
            }


            return containerGrid;
        }



        private void SetupMasukiMiraConversation1()
        {
            var conversation = new List<(string Text, VNStates Expression)>
    {
        ("…Masuki, do you really need to open all those windows?", VNStates.MiraNeutral),
        ("Yes! How else am I supposed to note my sparking genius?", VNStates.MasukiHappy),
        ("…I’m not sure sparkling genius is the right word here.", VNStates.MiraNeutral),
        ("Nonsense! Look at this! Chaotic yes but it's amazing?!", VNStates.MasukiStudious),
        ("…Chaotic, yes. Amazing? Not so much.", VNStates.MiraNeutral),
        ("Come on, don’t be so gloomy! Let’s make this fun~!", VNStates.MasukiHappy),
        ("Fun… I guess. Just… try not to break anything.", VNStates.MiraNeutral),
        ("Break things? Me? Impossible! I’m practically a software ninja!", VNStates.MasukiNerd),
        ("Yeah, like when you went Ninja Mode and broke my bed while trying to move it?", VNStates.MiraNeutral),
        ("THE WORK OF AN ENEMY STAND USER!", VNStates.MasukiDizzy),
        ("…Right, and i'm DIO.", VNStates.MiraNeutral),
        ("Anyway! Let’s get back to it!", VNStates.MasukiHappy),
        ("Genius Awaits NOBODY!", VNStates.MasukiNerd),
        ("Yup yup, let me just grab a drink.", VNStates.MiraNeutral),

    };

            SetDialogs(conversation);
        }






        public void LaunchVN()
        {
            string targetPath = "";
            string exeName = "DatabaseDesigner.exe";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                targetPath = Path.Combine(progFiles, @"Steam\steamapps\common\Database Designer\DatabaseDesigner-1.0-pc");

                if (!Directory.Exists(targetPath))
                {
                    targetPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\Database Designer-1.0-pc"));
                }
            }
            else
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] possiblePaths = {
                    ".local/share/Steam/steamapps/common/Database Designer/DatabaseDesigner-1.0-pc",
                    ".var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Database Designer/DatabaseDesigner-1.0-pc"
                };
                foreach (var p in possiblePaths)
                {
                    string check = Path.Combine(home, p);
                    if (Directory.Exists(check)) { targetPath = check; break; }
                }
            }

            if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
            {
                LaunchBtn.Content = "Not Installed!";
                return;
            }

            string exePath = Path.Combine(targetPath, exeName);

            if (!File.Exists(exePath))
            {

                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {

            }
        }

        public bool IsDatabaseDesignerVNInstalled()
        {
            string targetPath = "";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                targetPath = Path.Combine(progFiles, @"Steam\steamapps\common\Database Designer\DatabaseDesigner-1.0-pc");

                if (!Directory.Exists(targetPath))
                {
                    targetPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\Database Designer-1.0-pc"));
                }
            }
            else
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] possiblePaths = {
                    ".local/share/Steam/steamapps/common/Database Designer/DatabaseDesigner-1.0-pc",
                    ".var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Database Designer/DatabaseDesigner-1.0-pc"
                };
                foreach (var p in possiblePaths)
                {
                    string check = Path.Combine(home, p);
                    if (Directory.Exists(check))
                    {
                        targetPath = check;
                        break;
                    }
                }
            }

            return !string.IsNullOrEmpty(targetPath) && Directory.Exists(targetPath);
        }



    }
}
