using DatabaseDesigner;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using static Database_Designer.LogoutConfirm;

namespace Database_Designer
{
    public partial class DeleteProjectConfirm : Page
    {
        MainPage mainPaged;
        EditProjectData Viewer;
        public UIWindowEntry WindowInfo { get; private set; }

        public DeleteProjectConfirm(MainPage mainpage, EditProjectData viewer)
        {
            this.InitializeComponent();
            mainPaged = mainpage;
            Viewer = viewer;
            mainPaged.IntroPage.Children.Remove(this);

            string baseUrl = mainpage.baseUrl;


            No.Click += (s, e) =>
            {
                ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };
            };



            Yes.Click += async (s, e) =>
            {

                await DeleteCurrentProjectAsync();
            };






            ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };



            this.Unloaded += (s, e) =>
            {
                RemoveWindow();
            };



        }

        public async Task DeleteCurrentProjectAsync()
        {
            // Safety checks
            if (mainPaged == null)
            {
                Console.WriteLine("Main page reference is missing.");
                return;
            }

            var projName = mainPaged.ProjectName;
            if (string.IsNullOrEmpty(projName))
            {
                Console.WriteLine("No project is currently loaded.");
                return;
            }

            var projectDir = Path.Combine(
                mainPaged.SeshDirectory.ConvertToString(),
                mainPaged.SeshUsername.ConvertToString(),
                "Projects",
                projName);

            try
            {
                // Delete local project folder (run on background thread)
                if (Directory.Exists(projectDir))
                {
                    await Task.Run(() => Directory.Delete(projectDir, recursive: true));
                }

                // Clear the runtime session state
                mainPaged.RemoveTableUpdater();

                mainPaged.MainSessionInfo = new SessionStorage.DBDesignerSession()
                {
                    SessionName = "",
                    SessionDescription = "",
                    LastEdited = DateTime.MinValue,
                    SessionLogo = null,
                    Tables = new ObservableCollection<SessionStorage.TableObject>(),
                    WindowStatuses = new Dictionary<string, SessionStorage.Coords>()
                };

                mainPaged.ProjectName = null;


                try { mainPaged.Apps.Children.Clear(); } catch { }
                try { mainPaged.IntroPage.Children.Clear(); } catch { }

                mainPaged.SetupDefaultScreen();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete project '{projName}': {ex.Message}");
            }
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