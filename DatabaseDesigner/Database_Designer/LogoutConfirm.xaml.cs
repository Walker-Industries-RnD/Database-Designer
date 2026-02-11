using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Database_Designer
{
    public partial class LogoutConfirm : Page
    {
        MainPage mainPaged;
        public UIWindowEntry WindowInfo { get; private set; }

        public class ConneSessionReturnStr
        {
            public string Username { get; set; }
            public string SessionKey { get; set; }
            public string SessionID { get; set; }
            public string Directory { get; set; }
        }


        public LogoutConfirm(MainPage mainpage)
        {
            this.InitializeComponent();
            mainPaged = mainpage;
            mainPaged.IntroPage.Children.Remove(this);

            string baseUrl = mainpage.baseUrl;


            No.Click += (s,e) =>
            {
                ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };
            };



            Yes.Click += async (s, e) =>
            {

                No.Visibility = Visibility.Collapsed;
                Yes.Visibility = Visibility.Collapsed;

                Title.Text = "Logging Out...";
                Details.Text = "Thank You For Using Database Designer!";

                // Prepare logout payload (all plain strings)
                var logoutPayload = new ConneSessionReturnStr
                {
                    Username = mainPaged.SessionReturn.Username.ConvertToString(),
                    SessionKey = mainPaged.SessionReturn.SessionKey.ConvertToString(),
                    SessionID = mainPaged.SessionReturn.SessionID.ConvertToString(),
                    Directory = mainPaged.SeshDirectory.ConvertToString()
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(logoutPayload),
                    Encoding.UTF8,
                    "application/json"
                );

                // AES key as Base64 header
                var aesKey = mainPaged.Password.ConvertToString();
                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{baseUrl.TrimEnd('/')}/AccountsWithSessions/LogoutUser");
                request.Content = jsonContent;
                request.Headers.Add("X-AES-Key", Convert.ToBase64String(Encoding.UTF8.GetBytes(aesKey)));

                var response = await mainPaged.DBDesignerClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                mainPaged.UserLoggedOut();
            };






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
