using OpenSilver;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Database_Designer
{
    public partial class SplashScreen : Page
    {
        private MainPage mainPage;
        private DispatcherTimer videoCheckTimer;

        public SplashScreen(MainPage mainPaged)
        {
            InitializeComponent();
            mainPage = mainPaged;

            StartSplash();
        }

        private void StartSplash()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string introVideoPath = Path.Combine(baseDir, "wwwroot", "resources", "database_designer", "Assets", "Videos", "Intro.mp4");
            string splashVideoPath = Path.Combine(baseDir, "wwwroot", "resources", "database_designer", "Assets", "Videos", "Splash.mp4");

            bool hasIntro = File.Exists(introVideoPath);
            bool hasSplash = File.Exists(splashVideoPath);

            if (hasIntro || hasSplash)
            {
                string videoSrc = hasIntro ? "Assets/Videos/Intro.mp4" : "Assets/Videos/Splash.mp4";
                string fullPath = hasIntro ? introVideoPath : splashVideoPath;

                try
                {
                    byte[] videoBytes = File.ReadAllBytes(fullPath);
                    string base64 = Convert.ToBase64String(videoBytes);
                    string dataUrl = $"data:video/mp4;base64,{base64}";

                    OpenSilver.Interop.ExecuteJavaScript($@"
                        var video = document.createElement('video');
                        video.src = '{dataUrl}';
                        video.style.position = 'fixed';
                        video.style.top = '0';
                        video.style.left = '0';
                        video.style.width = '100vw';
                        video.style.height = '100vh';
                        video.style.objectFit = 'cover';
                        video.style.zIndex = '99999';
                        video.autoplay = true;
                        video.muted = false;
                        video.volume = 0.5;
                        video.loop = false;
                        video.controls = false;
                        document.body.appendChild(video);
                        video.onended = function() {{
                            document.body.removeChild(video);
                            if (window.startMainApp) window.startMainApp();
                        }};
                    ");

                    videoCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                    videoCheckTimer.Tick += (s, e) => CheckVideoFinished();
                    videoCheckTimer.Start();

                    var videoStatusText = new System.Windows.Controls.TextBlock
                    {
                        Text = "Playing intro...",
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    };

                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to play video: {ex.Message}");
                }
            }

            TransitionToMain();
        }

        private void CheckVideoFinished()
        {
            var finished = OpenSilver.Interop.ExecuteJavaScript(@"
                var videos = document.getElementsByTagName('video');
                if (videos.length > 0) {
                    if (videos[0].ended || videos[0].currentTime >= videos[0].duration - 0.1) {
                        true;
                    } else {
                        false;
                    }
                } else {
                    true;
                }
            ");

            if (finished is bool isFinished && isFinished)
            {
                videoCheckTimer?.Stop();
                TransitionToMain();
            }
        }

        private void TransitionToMain()
        {
            if (mainPage == null)
            {
                var mainP = new MainPage();
                var app = Application.Current as App;
                if (app != null)
                {
                    app.MainPage = mainP;
                    app.RootPage.NavigationService?.Navigate(mainP);
                }
            }
            else
            {
                mainPage.Visibility = Visibility.Visible;
            }

            if (this.Parent != null && this.Parent is Frame frame)
            {
                frame.Navigate(mainPage ?? new MainPage());
            }
            else if (Application.Current?.MainWindow is MainWindow mw)
            {
                mw.RootPage?.Navigate(mainPage ?? new MainPage());
            }
        }

        public static void CleanUp()
        {
            try
            {
                OpenSilver.Interop.ExecuteJavaScript(@"
                    var videos = document.getElementsByTagName('video');
                    for (var i = videos.length - 1; i >= 0; i--) {
                        videos[i].pause();
                        videos[i].remove();
                    }
                ");
            }
            catch { }
        }
    }
}