using ATL;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static Pariah_Cybersecurity.DataHandler.DataRequest;
namespace Database_Designer
{
    public partial class MusicPlayer : Page
    {
        private bool mustResumePlayback = false;
        private bool userDraggingSlider = false;

        public UIWindowEntry WindowInfo { get; private set; }

        private MainPage mainPage;

        private DispatcherTimer timer;
        private bool userDragging = false;
        private bool isPlaying = false;
        private static List<string> defaultPaths = new List<string>
{
    "Assets/Sounds/Album/Snow Covered Memories.mp3",
    "Assets/Sounds/Album/A Fragile Longing - Piano Ver.mp3",
    "Assets/Sounds/Album/A Fragile Longing.mp3",
    "Assets/Sounds/Album/BloodStainedEthics.mp3",
    "Assets/Sounds/Album/Confabulation.mp3",
    "Assets/Sounds/Album/Dreglord's Castle - Forgotten.mp3",
    "Assets/Sounds/Album/Dreglord's Castle - Memories.mp3",
    "Assets/Sounds/Album/Dreglord's Castle - Reminisce.mp3",
    "Assets/Sounds/Album/Dreglord's Pyre - Ascent.mp3",
    "Assets/Sounds/Album/Dreglord's Pyre - Summit.mp3",
    "Assets/Sounds/Album/E.R.I.C. PROTOCOL.mp3",
    "Assets/Sounds/Album/Existence Unwilling.mp3",
    "Assets/Sounds/Album/Memory Shaped Coffin - Possession.mp3",
    "Assets/Sounds/Album/Memory Shaped Coffin - Sin Covered Longing.mp3",
    "Assets/Sounds/Album/Pariah's Tomb.mp3",
    "Assets/Sounds/Album/Raindrops From Data.mp3", 
    "Assets/Sounds/Album/Sanguine Cathedral.mp3",
    "Assets/Sounds/Album/Silent Departure.mp3",
    "Assets/Sounds/Album/The Loss of Personification.mp3",
    "Assets/Sounds/Album/Trance.mp3"
};
        public MusicPlayer(MainPage mainPaged)
        {
            InitializeComponent();
            mainPage = mainPaged;
            ProgressSlider.DragEnter += (s,e) => ProgressSlider_DragStarted();
            ProgressSlider.DragLeave += (s, e) => ProgressSlider_DragCompleted();
            ProgressSlider.ValueChanged += ProgressSlider_ValueChanged;
            ProgressSlider.PreviewMouseDown += ProgressSlider_PreviewMouseDown;
            ProgressSlider.PreviewMouseUp += ProgressSlider_PreviewMouseUp;

            if (mainPage.BGM.CurrentState == MediaElementState.Playing)
            {
                PlayPauseImg.Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/pause.png", UriKind.Relative));
            }
            else
            {
                PlayPauseImg.Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/play.png", UriKind.Relative));
            }

            isPlaying = mainPage.BGM.CurrentState == MediaElementState.Playing;

            bool albumSong = mainPage?.BGM?.Source != null &&
                defaultPaths.Any(path => mainPage.BGM.Source.ToString()
                    .Contains(path, StringComparison.OrdinalIgnoreCase));

            if (albumSong)
            {
                AlbumCoverBrush.ImageSource = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/becomeasdustcover.png", UriKind.Relative));
                SongImgBG.Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/becomeasdustcover.png", UriKind.Relative));
                mainPage.currentPlaylist = mainPage.defaultSongs;
            }
            else
            {
                mainPage.currentPlaylist = mainPage.customSongs;
                {
                    string filePath = mainPage.BGM.Source.AbsolutePath;
                    var allowedExtensions = new List<string> { ".jpg", ".jpeg", ".gif", ".png" };
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    try
                    {
                        var matchingFiles = Directory.EnumerateFiles(filePath, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(file =>
                                Path.GetFileNameWithoutExtension(file).StartsWith(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase) &&
                                allowedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
                            );
                        if (matchingFiles.Any())
                        {
                            try
                            {
                                byte[] imageBytes = File.ReadAllBytes(matchingFiles.First());
                                var bitmap = new BitmapImage();
                                using (var stream = new MemoryStream(imageBytes))
                                {
                                    bitmap.SetSource(stream);
                                }
                                AlbumCoverBrush.ImageSource = bitmap;
                                SongImgBG.Source = bitmap;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to load banner: {ex.Message}");
                            }
                        }
                        else
                        {
                            var track = new Track(filePath);
                            if (track.EmbeddedPictures != null && track.EmbeddedPictures.Any())
                            {
                                var picture = track.EmbeddedPictures.First();
                                byte[] imageData = picture.PictureData;
                                var formattedImg = GetImageFromBytes(imageData);
                                SongImgBG.Source = formattedImg;
                                AlbumCoverBrush.ImageSource = formattedImg;
                            }
                            else
                            {
                                SongImgBG.Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/notfound.png", UriKind.Relative));
                                AlbumCoverBrush.ImageSource = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/notfound.png", UriKind.Relative));
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (mainPage.BGM.Source == null)
            {
                SongTitle.Text = "No Song Loaded.";
                ArtistName.Text = "How Strange...";
            }
            else
            {
                var songName = mainPage.BGM.Source.ToString();
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = Path.Combine(baseDir, "wwwroot", "resources", "database_designer", songName);
                Console.WriteLine("Full Song: " + filePath);
                Track trackData = new Track(filePath);
                if (albumSong)
                {
                    SongTitle.Text = Path.GetFileNameWithoutExtension(filePath);
                    ArtistName.Text = "WalkerDev";
                }
                else
                {
                    SongTitle.Text = trackData.Title;
                    ArtistName.Text = trackData.Artist;
                }
            }

            ProgressSlider.Minimum = 0;
            ProgressSlider.Maximum = 1;
            ProgressSlider.Value = 0;

            if (mainPage.BGM.Source == null)
            {
                TotalTime.Text = "00:00";
                TimerTick.Text = "00:00";
            }

            else
            {
                var songName = mainPage.BGM.Source.ToString();
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = Path.Combine(baseDir, "wwwroot", "resources", "database_designer", songName);
                Console.WriteLine("Full Song: " + filePath);
                Track trackData = new Track(filePath);

                var Duration = trackData.Duration;

                int minutes = (int)Math.Floor((double)((int)Duration / 60));
                int seconds = (int)Math.Floor((double)((int)Duration % 60));

                string mm = minutes < 10 ? "0" + minutes : minutes.ToString();
                string ss = seconds < 10 ? "0" + seconds : seconds.ToString();

                var totalTime = mm + ":" + ss;

                TotalTime.Text = totalTime;
            }

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(0.2);
            timer.Tick += Timer_Tick;
            timer.Start();
            if (!mainPage.BGMLogicAdded)
            {
                mainPage.BGM.MediaEnded += BGM_MediaEnded;
                mainPage.BGMLogicAdded = true;
            }

            LoadAlbumSongs();
            LoadCustomSongs();
            Loop.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mainPage.BGM.IsLooping ? "#FF837349" : "#FF9D9785"));
            Shuffle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mainPage.isShuffle ? "#FF837349" : "#FF9D9785"));
            Loop.Click += (s, e) => ToggleLoop();
            Shuffle.Click += (s, e) => ToggleShuffle();
            Music.Visibility = Visibility.Collapsed;
            SongList.Click += (s, e) => ToggleSongList();
            ExitButton.Click += (s, e) => ClosePlayer();
            PlayPause.Click += (s, e) => TogglePlayPause();
            mainPage.BGM.MediaOpened += BGM_MediaOpened;
            mainPage.BGM.MediaFailed += (s, e) =>
            {
                Console.WriteLine($"BGM MediaFailed: {e.ErrorException?.Message}");
            };
            Forth.Click += (s, e) => PlayNextSong();
            Back.Click += (s, e) => PlayPreviousSong();

            if (mainPage.BGM.Source != null && mainPage.BGM.NaturalDuration.HasTimeSpan)
            {
                BGM_MediaOpened(null, null);
            }

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
        

        private void BGM_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (mainPage.BGM.Source == null) return;

            var songName = mainPage.BGM.Source.ToString();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = Path.Combine(baseDir, "wwwroot", "resources", "database_designer", songName);

            Console.WriteLine("Full Song: " + filePath);

            try
            {
                Track trackData = new Track(filePath);
                var Duration = trackData.Duration;

                int minutes = (int)Math.Floor((double)((int)Duration / 60));
                int seconds = (int)Math.Floor((double)((int)Duration % 60));
                string mm = minutes < 10 ? "0" + minutes : minutes.ToString();
                string ss = seconds < 10 ? "0" + seconds : seconds.ToString();
                var totalTime = mm + ":" + ss;
                TotalTime.Text = totalTime;

                double atlDurationSeconds = Duration;

                if (atlDurationSeconds > 0)
                {
                    ProgressSlider.Maximum = atlDurationSeconds;
                    ProgressSlider.Minimum = 0;

                    if (!userDraggingSlider)
                    {
                        double current = GetCurrentPositionInSeconds();
                        ProgressSlider.Value = current;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading track info: {ex.Message}");
                ProgressSlider.Maximum = 1;
                ProgressSlider.Minimum = 0;
                ProgressSlider.Value = 0;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (mainPage.BGM.Source == null)
            {
                TimerTick.Text = "00:00";
                ProgressSlider.Value = 0;
                return;
            }

            if (!userDraggingSlider)
            {
                double currentSeconds = GetCurrentPositionInSeconds();

                if (!double.IsNaN(ProgressSlider.Maximum) &&
                    !double.IsInfinity(ProgressSlider.Maximum) &&
                    ProgressSlider.Maximum > 0)
                {
                    ProgressSlider.Value = currentSeconds;
                }

                TimeSpan pos = TimeSpan.FromSeconds(currentSeconds);
                int minutes = (int)pos.TotalMinutes;
                int seconds = pos.Seconds;
                TimerTick.Text =
                    (minutes < 10 ? "0" : "") + minutes + ":" +
                    (seconds < 10 ? "0" : "") + seconds;
            }
        }

        private void ProgressSlider_DragStarted()
        {
            if (mainPage.BGM.Source == null) return;
            userDraggingSlider = true;
            if (mainPage.BGM.CurrentState == MediaElementState.Playing)
            {
                mustResumePlayback = true;
                mainPage.BGM.Pause();
            }
        }

        private void ProgressSlider_DragCompleted()
        {
            if (mainPage.BGM.Source == null) return;

            var audio = GetNativeAudioElement();
            if (audio != null)
            {
                OpenSilver.Interop.ExecuteJavaScript("$0.currentTime = $1", audio, ProgressSlider.Value);
            }

            if (mustResumePlayback)
            {
                mainPage.BGM.Play();
                mustResumePlayback = false;
            }

            userDraggingSlider = false;
        }

        private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (mainPage.BGM.Source == null) return;

            userDraggingSlider = true;
            if (mainPage.BGM.CurrentState == MediaElementState.Playing)
            {
                mustResumePlayback = true;
                mainPage.BGM.Pause();
            }
        }

        private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (mainPage.BGM.Source == null) return;

            var audio = GetNativeAudioElement();
            if (audio != null)
            {
                OpenSilver.Interop.ExecuteJavaScript("$0.currentTime = $1", audio, ProgressSlider.Value);
            }

            if (mustResumePlayback)
            {
                mainPage.BGM.Play();
                mustResumePlayback = false;
            }

            userDraggingSlider = false;
        }

        private void BGM_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (mainPage.BGM.IsLooping)
            {
                mainPage.BGM.Position = TimeSpan.Zero;
                mainPage.BGM.Play();
                return;
            }

            PlayNextSong();
        }

        private void PlayPreviousSong()
        {
            if (mainPage.currentPlaylist == null || mainPage.currentPlaylist.Count == 0) return;

            int currentIndex = mainPage.currentPlaylist.IndexOf(mainPage.BGM.Source?.OriginalString ?? "");
            if (currentIndex == -1) return;

            int previousIndex;
            if (mainPage.isShuffle)
            {
                previousIndex = currentIndex == 0 ? mainPage.currentPlaylist.Count - 1 : currentIndex - 1;
            }
            else
            {
                previousIndex = currentIndex == 0 ? mainPage.currentPlaylist.Count - 1 : currentIndex - 1;
            }

            string previousSong = mainPage.currentPlaylist[previousIndex];
            mainPage.BGM.Source = new Uri(previousSong, UriKind.RelativeOrAbsolute);
            mainPage.BGM.Play();
            UpdateNowPlayingUI(previousSong);
        }

        private void PlayNextSong()
        {
            if (mainPage.currentPlaylist == null || mainPage.currentPlaylist.Count == 0) return;

            int currentIndex = mainPage.currentPlaylist.IndexOf(mainPage.BGM.Source?.OriginalString ?? "");
            if (currentIndex == -1) return;

            string nextSong;
            if (mainPage.isShuffle)
            {
                var random = new Random();
                do
                {
                    nextSong = mainPage.currentPlaylist[random.Next(mainPage.currentPlaylist.Count)];
                } while (mainPage.currentPlaylist.Count > 1 && nextSong == mainPage.BGM.Source?.OriginalString);
            }
            else
            {
                int nextIndex = (currentIndex + 1) % mainPage.currentPlaylist.Count;
                nextSong = mainPage.currentPlaylist[nextIndex];
            }

            mainPage.BGM.Source = new Uri(nextSong, UriKind.RelativeOrAbsolute);


            mainPage.BGM.Play();
            UpdateNowPlayingUI(nextSong);
        }

        private void UpdateNowPlayingUI(string songPath)
        {
            Track track = new Track(songPath);
            bool isAlbum = defaultPaths.Any(p => songPath.Contains(p, StringComparison.OrdinalIgnoreCase));

            var songName = mainPage.BGM.Source.ToString();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = Path.Combine(baseDir, "wwwroot", "resources", "database_designer", songName);
            Console.WriteLine("Full Song: " + filePath);
            Track trackData = new Track(filePath);

            var Duration = trackData.Duration;

            int minutes = (int)Math.Floor((double)((int)Duration / 60));
            int seconds = (int)Math.Floor((double)((int)Duration % 60));

            string mm = minutes < 10 ? "0" + minutes : minutes.ToString();
            string ss = seconds < 10 ? "0" + seconds : seconds.ToString();

            var totalTime = mm + ":" + ss;

            TotalTime.Text = totalTime;

            if (isAlbum)
            {
                SongTitle.Text = Path.GetFileNameWithoutExtension(songPath);
                ArtistName.Text = "WalkerDev";
                AlbumCoverBrush.ImageSource = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/becomeasdustcover.png", UriKind.Relative));
                SongImgBG.Source = AlbumCoverBrush.ImageSource;
            }
            else
            {
                SongTitle.Text = track.Title ?? Path.GetFileNameWithoutExtension(songPath);
                ArtistName.Text = track.Artist ?? "";
            }
        }

        private void LoadAlbumSongs()
        {
            foreach (var song in defaultPaths)
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = Path.Combine(baseDir, "wwwroot", "resources", "database_designer", song);
                var songInfo = new Track(fullPath);
                var songName = songInfo.Title ?? Path.GetFileNameWithoutExtension(song);
                var songArtist = "WalkerDev";
                var duration = songInfo.Duration;
                mainPage.defaultSongs.Add(song);
                var btn = CreateSongButton(songName, songArtist, duration, true, song);
                Album1.Children.Add(btn);
            }
        }

        private void LoadCustomSongs()
        {
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".flac", ".aac", ".m4a" };
            var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string userMusic = string.Empty;
            try
            {
                userMusic = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            }
            catch { userMusic = string.Empty; }

            if (string.IsNullOrWhiteSpace(userMusic) || !Directory.Exists(userMusic)) return;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(userMusic, "*.*", SearchOption.TopDirectoryOnly); }
            catch { return; }

            foreach (var file in files)
            {
                try
                {
                    if (!allowedExtensions.Contains(Path.GetExtension(file))) continue;
                    var fullPath = Path.GetFullPath(file);
                    if (added.Contains(fullPath)) continue;

                    var songInfo = new Track(fullPath);
                    var songName = songInfo.Title ?? Path.GetFileNameWithoutExtension(fullPath);
                    var songArtist = songInfo.Artist ?? "";
                    var duration = songInfo.Duration;

                        // Store a file:// URI string in the playlist so MediaElement.Source.OriginalString matches
                        var fileUri = new Uri(fullPath).OriginalString;
                        mainPage.customSongs.Add(fileUri);
                        var btn = CreateSongButton(songName, songArtist, duration, false, fullPath);
                    CustomSongsList.Children.Add(btn);
                    added.Add(fullPath);
                }
                catch { /* ignore individual file issues */ }
            }
        }



        private Button CreateSongButton(string SongName, string SongArtist, double Duration, bool isAlbumSong, string songPath)
        {
            var button = new Button
            {
                Height = 55,
                Padding = new Thickness(6),
                Background = new SolidColorBrush(Color.FromArgb(255, 35, 35, 35))
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            BitmapImage imgToUse = default;
            if (isAlbumSong)
            {
                imgToUse = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/becomeasdustcover.png", UriKind.Relative));
            }
            else
            {
                string directory = Path.GetDirectoryName(songPath);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(songPath);
                var allowedExtensions = new List<string> { ".jpg", ".jpeg", ".gif", ".png" };
                try
                {
                    var matchingFiles = Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file =>
                    Path.GetFileNameWithoutExtension(file).StartsWith(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase) &&
                    allowedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
                    );
                    if (matchingFiles.Any())
                    {
                        try
                        {
                            byte[] imageBytes = File.ReadAllBytes(matchingFiles.First());
                            var bitmap = new BitmapImage();
                            using (var stream = new MemoryStream(imageBytes))
                            {
                                bitmap.SetSource(stream);
                            }
                            imgToUse = bitmap;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to load banner: {ex.Message}");
                        }
                    }
                    else
                    {
                        var track = new Track(songPath);
                        if (track.EmbeddedPictures != null && track.EmbeddedPictures.Any())
                        {
                            var picture = track.EmbeddedPictures.First();
                            byte[] imageData = picture.PictureData;
                            var formattedImg = GetImageFromBytes(imageData);
                            imgToUse = formattedImg;
                        }
                        else
                        {
                            imgToUse = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/notfound.png", UriKind.Relative));
                        }
                    }
                }
                catch
                {
                }
            }
            var image = new Image
            {
                Width = 45,
                Height = 45,
                Stretch = Stretch.UniformToFill,
                VerticalAlignment = VerticalAlignment.Center,
                Source = imgToUse
            };
            Grid.SetColumn(image, 0);
            grid.Children.Add(image);
            var textPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 8, 0),
                Width = 206
            };
            Grid.SetColumn(textPanel, 1);
            textPanel.Children.Add(new TextBlock
            {
                Text = SongName,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Width = 206,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf#Inter")
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = SongArtist,
                FontSize = 12,
                Opacity = 0.7,
                Width = 206,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf#Inter")
            });
            grid.Children.Add(textPanel);
            var minutes = Math.Floor(Duration / 60);
            var seconds = Math.Floor(Duration % 60);

            string mm = minutes < 10 ? "0" + minutes : minutes.ToString();
            string ss = seconds < 10 ? "0" + seconds : seconds.ToString();

            var totalTime = mm + ":" + ss;

            var durationText = new TextBlock
            {
                Text = totalTime,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
                Margin = new Thickness(8, 0, 4, 0)
            };
            Grid.SetColumn(durationText, 2);
            grid.Children.Add(durationText);
            button.Content = grid;
            button.Click += (s, e) =>
            {
                try
                {
                    // Ensure MediaElement stops before changing source
                    try { mainPage.BGM.Stop(); } catch { }

                    Uri srcUri;
                    if (Path.IsPathRooted(songPath))
                    {
                        srcUri = new Uri(songPath, UriKind.Absolute);
                    }
                    else
                    {
                        srcUri = new Uri(songPath, UriKind.RelativeOrAbsolute);
                    }

                    Console.WriteLine($"Setting BGM.Source = {srcUri}");
                    mainPage.BGM.Source = srcUri;
                    Console.WriteLine($"BGM state before Play: {mainPage.BGM.CurrentState}");

                    SongTitle.Text = SongName;
                    ArtistName.Text = SongArtist;
                    AlbumCoverBrush.ImageSource = imgToUse;
                    SongImgBG.Source = imgToUse;
                    ProgressSlider.Value = 0;
                    TimerTick.Text = "00:00";

                    mainPage.BGM.Play();
                    Console.WriteLine($"BGM state after Play: {mainPage.BGM.CurrentState}");

                    isPlaying = true;
                    PlayPauseImg.Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/pause.png", UriKind.Relative));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to play '{songPath}': {ex.Message}");
                }
            };
            return button;
        }
        public static System.Windows.Media.Imaging.BitmapImage GetImageFromBytes(byte[] rawImageBytes)
        {
            if (rawImageBytes == null || rawImageBytes.Length == 0) return null;
            System.Windows.Media.Imaging.BitmapImage imageSource = null;
            try
            {
                using (System.IO.MemoryStream stream = new System.IO.MemoryStream(rawImageBytes))
                {
                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    System.Windows.Media.Imaging.BitmapImage b = new BitmapImage();
                    b.SetSource(stream);
                    imageSource = b;
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error converting bytes to BitmapImage: {ex.Message}");
            }
            return imageSource;
        }



        private void TogglePlayPause()
        {
            if (mainPage.BGM.Source == null) return;
            if (isPlaying)
            {
                mainPage.BGM.Pause();
                PlayPauseImg.Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/play.png", UriKind.Relative));
            }
            else
            {
                mainPage.BGM.Play();
                PlayPauseImg.Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/volumeui/pause.png", UriKind.Relative));
            }
            isPlaying = !isPlaying;
        }
        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (userDraggingSlider)
            {
                TimeSpan pos = TimeSpan.FromSeconds(e.NewValue);
                int minutes = (int)pos.TotalMinutes;
                int seconds = pos.Seconds;
                TimerTick.Text =
                    (minutes < 10 ? "0" : "") + minutes + ":" +
                    (seconds < 10 ? "0" : "") + seconds;
            }
        }


        private void ToggleLoop()
        {
            mainPage.BGM.IsLooping = !mainPage.BGM.IsLooping;
            mainPage.isShuffle = false;
            Loop.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mainPage.BGM.IsLooping ? "#FF837349" : "#FF9D9785"));
            Shuffle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9D9785"));
        }
        private void ToggleShuffle()
        {
            mainPage.isShuffle = !mainPage.isShuffle;
            mainPage.BGM.IsLooping = false;
            Shuffle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mainPage.isShuffle ? "#FF837349" : "#FF9D9785"));
            Loop.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9D9785"));
        }
        private void ToggleSongList()
        {
            if (SongList.Content.ToString() == ">")
            {
                SongList.Content = "<";
                Music.Visibility = Visibility.Visible;
            }
            else
            {
                SongList.Content = ">";
                Music.Visibility = Visibility.Collapsed;
            }
        }
        private void ClosePlayer()
        {
            mainPage.mpSpawned = false;

            try { if (mainPage.IntroPage.Children.Contains(this)) mainPage.IntroPage.Children.Remove(this); }
            catch (ArgumentOutOfRangeException) { }
        }
























        private object GetAudioDomElement()
        {
            return OpenSilver.Interop.GetDiv(mainPage.BGM);
        }

        private object GetNativeAudioElement()
        {
            var outerDiv = GetAudioDomElement();
            if (outerDiv == null) return null;
            return OpenSilver.Interop.ExecuteJavaScript("$0.children[0]", outerDiv);
        }

        private double GetCurrentPositionInSeconds()
        {
            var audio = GetNativeAudioElement();
            if (audio == null) return 0.0;
            return Convert.ToDouble(OpenSilver.Interop.ExecuteJavaScript("$0.currentTime", audio));
        }

        private double GetDurationInSeconds()
        {
            var audio = GetNativeAudioElement();
            if (audio == null) return 1.0;

            object result = OpenSilver.Interop.ExecuteJavaScript("$0.duration", audio);
            if (result is double duration && !double.IsNaN(duration) && !double.IsInfinity(duration))
                return duration;

            return 1.0;
        }



    }
}