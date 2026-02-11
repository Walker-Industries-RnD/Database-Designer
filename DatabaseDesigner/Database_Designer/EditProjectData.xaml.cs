using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace Database_Designer
{
    public partial class EditProjectData : Page
    {
        MainPage mainPaged;

        public UIWindowEntry WindowInfo { get; private set; }


        public EditProjectData(MainPage mainpage)
        {
            this.InitializeComponent();

            mainPaged = mainpage;


            try
            {

                var CurrentDirectory = Path.Combine(mainPaged.SeshDirectory.ConvertToString(), mainPaged.SeshUsername.ConvertToString(), "Projects", mainPaged.ProjectName);

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
                        Preview.Source = bitmap;
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



            ChangeImage.Click += (s, e) =>
            {
                ImageHelper.SelectAndPreviewImage(Preview, async bytes =>
                {
                    var CurrentDirectory = Path.Combine(mainPaged.SeshDirectory.ConvertToString(), mainPaged.SeshUsername.ConvertToString(), "Projects", mainPaged.ProjectName);

                    Console.WriteLine(CurrentDirectory);


                    //Remove all files within here

                    if (Directory.Exists(CurrentDirectory))
                    {
                        var wallpaperFiles = Directory.GetFiles(CurrentDirectory, "ProjectIcon.*");
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



                    // Get the image format extension
                    string extension = GetImageFormat(bytes);
                    string iconPath = Path.Combine(CurrentDirectory, $"ProjectIcon.{extension}");

                    try
                    {
                        // Delete all files in the directory that start with "Wallpaper." (case-insensitive)
                        var existingWallpapers = Directory.GetFiles(CurrentDirectory, "ProjectIcon.*", SearchOption.TopDirectoryOnly)
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

                        // Write the new Project Icon
                        File.WriteAllBytes(iconPath, bytes);
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


            ProjectName.Text = mainpage.MainSessionInfo.SessionName;
            Desc.Text = mainpage.MainSessionInfo.SessionDescription;

            Save.Click += (s, e) =>
            {
                mainpage.MainSessionInfo.SessionName = ProjectName.Text;
                mainpage.MainSessionInfo.SessionDescription = Desc.Text;

                mainpage.SetProjectImg();
                mainpage.ForceCollectionChangeUpate();

                try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { }
            };

            ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };

            Cancel.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };

   

            DeleteProject.Click += (s, e) =>
            {
                mainPaged.CreateWindow(() => new DeleteProjectConfirm(mainPaged, this), "Delete Project", true);
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


        // Executes when the user navigates to this page.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }
    }
}