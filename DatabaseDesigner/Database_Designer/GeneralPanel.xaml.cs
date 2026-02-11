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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace Database_Designer
{
    public partial class GeneralPanel : Page
    {
        static string baseUrl;

        public GeneralPanel(MainPage mainPaged)
        {
            this.InitializeComponent();

            baseUrl = mainPaged.baseUrl;

            ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };

            var CurrentDirectory = Path.Combine(mainPaged.SeshDirectory.ConvertToString(), mainPaged.SeshUsername.ConvertToString(), "Projects", mainPaged.ProjectName);

            try
            {
                var logoImg = Directory.GetFiles(CurrentDirectory, "ProjectIcon.*")[0];


                if (!string.IsNullOrEmpty(logoImg))
                {
                    try
                    {
                        byte[] imageBytes = File.ReadAllBytes(logoImg);
                        var bitmap = new BitmapImage();
                        using (var stream = new MemoryStream(imageBytes))
                        {
                            bitmap.SetSource(stream);
                        }
                        ProjectImg.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load banner: {ex.Message}");
                    }
                }

            }

            catch
            {
            }

            mainPage = mainPaged;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Initial display
            UpdateTime();


            //Update Text
            UpdateProjectInfo();

            ClearScreen.Click += (s, e) =>
            {
                mainPage.IntroPage.Children.Clear();
                mainPage.LowerAppBar.Children.Clear();
            };

            EditProjectInfo.Click += (s, e) =>
            {
                mainPage.CreateWindow((() => new EditProjectData(mainPage)), "Edit Project", true);
            };


            SetWallpaper.Click += (s, e) =>
            {
                ImageHelper.SelectAndPreviewImage(mainPage.MainBG, async bytes =>
                {
                    var CurrentDirectory = Path.Combine(mainPaged.SeshDirectory.ConvertToString(), mainPaged.SeshUsername.ConvertToString(), "Projects", mainPaged.ProjectName);

                    //Remove all files within here

                    if (Directory.Exists(CurrentDirectory))
                    {
                        var wallpaperFiles = Directory.GetFiles(CurrentDirectory, "Wallpaper.*");
                        foreach (var file in wallpaperFiles)
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to delete {file}: {ex.Message}");
                            }
                        }
                    }


                    //Load to path

                    var createDirUrl = $"{baseUrl}Folder/CreateDirectory?directory={Uri.EscapeDataString(CurrentDirectory)}";
                    var createDirResponse = await mainPage.DBDesignerClient.PostAsync(createDirUrl, null);
                    createDirResponse.EnsureSuccessStatusCode();


                    //Add to file

                    // Get the image format extension
                    string extension = GetImageFormat(bytes);
                    string newWallpaperPath = Path.Combine(CurrentDirectory, $"Wallpaper.{extension}");

                    try
                    {
                        // Delete all files in the directory that start with "Wallpaper." (case-insensitive)
                        var existingWallpapers = Directory.GetFiles(CurrentDirectory, "Wallpaper.*", SearchOption.TopDirectoryOnly)
                            .Where(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                           file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                           file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                           file.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                                           file.EndsWith(".gif", StringComparison.OrdinalIgnoreCase));

                        foreach (var existingWallpaper in existingWallpapers)
                        {
                            try
                            {
                                File.Delete(existingWallpaper);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Could not delete old wallpaper {existingWallpaper}: {ex.Message}");
                                // Continue with other files
                            }
                        }

                        // Write the new wallpaper
                        File.WriteAllBytes(newWallpaperPath, bytes);
                    }
                    catch (Exception ex)
                    {
                        // Handle any errors during the process
                        Console.WriteLine($"Error saving wallpaper: {ex.Message}");
                        throw;
                    }



                });
            };


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
        

        public UIWindowEntry WindowInfo { get; private set; }

        MainPage mainPage;

        private DispatcherTimer _timer;


        // Executes when the user navigates to this page.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }



        string FormatNumber(double number)
        {
            if (number >= 1_000_000_000)
                return (number / 1_000_000_000D).ToString("0.##") + "B";
            if (number >= 1_000_000)
                return (number / 1_000_000D).ToString("0.##") + "M";
            if (number >= 1_000)
                return (number / 1_000D).ToString("0.##") + "k";
            return number.ToString("0");
        }

        string GetImageFormat(byte[] bytes)
        {
            if (bytes.Length < 8)
                return "Unknown";

            // PNG
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return "PNG";

            // JPEG
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return "JPEG";

            // GIF
            if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
                return "GIF";

            // BMP
            if (bytes[0] == 0x42 && bytes[1] == 0x4D)
                return "BMP";

            return "Unknown";
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateTime();
        }

        private void UpdateTime()
        {
            DateTime now = DateTime.Now;

            string time = now.ToString("hh:mm");
            TIme.Text = time;


            if (now.Hour >= 12)
            {
                TimeXM.Text = "PM";
            }

            else
            {
                TimeXM.Text = "AM";
            }

            string date = now.ToString("MMMM d yyyy");
            Date.Text = date;

            Seconds.Text = now.ToString("ss");

        }

        private void UpdateProjectInfo()
        {
            var Data = mainPage.MainSessionInfo;

            Name.Text = $" ↳  Name: {Data.SessionName}";

            string DescText;

            if (Data.SessionDescription.Count() >= 69)
            {
                string tempText = Data.SessionDescription.Substring(0, 69);

                DescText = tempText + "...";

            }

            else
            {
                DescText = Data.SessionDescription;
            }    

                Desc.Text = $" ↳  Description: {DescText}";

           var lastEditedTime = DateTime.Parse(Data.LastEdited.ToString());

            LastEdited.Text = lastEditedTime.ToString("f");

            Hash.Text = Data.GetHashCode().ToString();

            TableCount.Text = $"↳ Table Count Count: {Data.Tables.Count.ToString()}";

            var RCount = 0;

            foreach (var item in Data.Tables)
            {
                RCount = + item.Rows.Count();
            }

            RowCount.Text = $"↳ Row Count: {FormatNumber(RCount)}";

            var ReCount = 0;

            foreach (var item in Data.Tables)
            {
                if (item.References != null)
                {
                    ReCount =+ item.References.Count();
                }
            }

            RefCount.Text = $"↳ Reference Count: {FormatNumber(ReCount)}";

            Username.Text = $"↳ Username: {mainPage.SeshUsername.ConvertToString()}";

            var CurrentDirectory = Path.Combine(mainPage.SeshDirectory.ConvertToString(), mainPage.SeshUsername.ConvertToString(), "Projects");

            var projectsCount = Directory.GetDirectories(CurrentDirectory).Count();

            ProjectsCount.Text = $"↳ Total Amount Of Projects: {projectsCount}";

            var userFile = Path.Combine(mainPage.SeshDirectory.ConvertToString(), mainPage.SeshUsername.ConvertToString(), "PublicUserData.JSON");

            var accountCreatedOn = File.GetCreationTime(userFile);

            AccCreated.Text = $"↳ Account Created On: {accountCreatedOn.ToString("f")}";



        }








    }
}
