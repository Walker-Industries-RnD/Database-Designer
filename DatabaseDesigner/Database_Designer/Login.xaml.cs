using DatabaseDesigner;
using NAudio.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.X509;
using Pariah_Cybersecurity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using WISecureData;
using static Pariah_Cybersecurity.DataHandler;
using static Pariah_Cybersecurity.DataHandler.AccountsWithSessions;
using JsonSerializer = System.Text.Json.JsonSerializer;
using SecuritySettings = Pariah_Cybersecurity.DataHandler.AccountsWithSessions.SecuritySettings;


namespace Database_Designer
{
    public partial class Login : Page
    {



        internal static SecureData Username;
        internal static SecureData Password;
        internal static SecureData Directory;
        internal static SecureData PFP;
        internal static ConnectedSessionReturn Session;
        public UIWindowEntry WindowInfo { get; private set; }

        System.Windows.Controls.UIElementCollection childrenElements;

        private static MainPage mainPage;
        public Login(MainPage mainPaged)
        {
            this.InitializeComponent();
            mainPage = mainPaged;
            childrenElements = LoginMain.Children;


            SetupStartPage();

            ExitButton.Click += (s, e) =>
            {
                try
                {
                    if (mainPaged.IntroPage.Children.Contains(this))
                        mainPaged.IntroPage.Children.Remove(this);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // I have been trying to understand why this happens, but I can't figure it out!
                    // It started out of nowhere , and I can't find any logical reason for it to be happening.
                    // Thus, i'm placing this timer below! Please add how much time you lost to this :)

                    //Hours Lost: 3

                    //Praise be to Space King

                }
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


        // Executes when the user navigates to this page.  
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        public static void InitializeMainSoftware()
        {
            mainPage.UserLoggedIn(Username, Password, Directory, Session);

            Username = default;
            Password = default;
            Directory = default;
            Session = default;

        }


        public void GoToLoadingScreen()
        {

        }

        public void EndLoadingScreen()
        {

        }

        public void SetupStartPage()
        {
            foreach (var child in childrenElements)
            {
                if (child is FrameworkElement fe && fe.Name != "Opening" && fe.Name != "BG")
                {
                    child.Visibility = Visibility.Collapsed;
                }
                else
                {
                    child.Visibility = Visibility.Visible;
                }
            }

            Accounts.IsVisibleChanged += (sender, e) =>
            {
                if (Accounts.IsVisible == true)
                {
                    PopulateUsersPage();
                }
            };


            OpeningButton.Click += GoToSignUp;


            var setupPlace = Path.Combine(Environment.CurrentDirectory, "DatabaseDesignerData");

            var fileExists = File.Exists(Path.Combine(setupPlace, "Users.json"));


            // 2. If file or directory does not exist, create folder and run setup
            if (!fileExists)
            {
                var ACS = new Pariah_Cybersecurity.DataHandler.AccountsWithSessions();

                System.IO.Directory.CreateDirectory(setupPlace);

                ACS.SetupFiles(setupPlace);

            }





            var Tab1 = CreateBasicsTab(CreateAccTabControl);
            var Tab2 = CreateProfileImageTab(CreateAccTabControl);
            var Tab3 = CreateAccountOverviewTab(CreateAccTabControl);

            CreateAccTabControl.Items.Add(Tab1);
            CreateAccTabControl.Items.Add(Tab2);
            CreateAccTabControl.Items.Add(Tab3);

            CreateAccTabControl.TabIndex = 0;


            PasswordLogin.Click += async (s, e) =>
            {

                PasswordLogin.IsEnabled = false;
                CancelLogin.IsEnabled = false;
                PasswordLogin.Content = "Logging In...";


                var baseDir = AppContext.BaseDirectory;
                var dbDesignDir = Path.Combine(baseDir, "DatabaseDesignerData");


                var usernameStr = Username.ConvertToString();
                var passwordStr = PasswordInputLogin.Password;


                try
                {

                    var ACS = new Pariah_Cybersecurity.DataHandler.AccountsWithSessions();

                    var loginResponse = await ACS.LoginUser(usernameStr, dbDesignDir, passwordStr.ToSecureData(), true);


                    SignIn.Visibility = Visibility.Collapsed;
                    Welcome.Visibility = Visibility.Visible;


                    Directory = loginResponse.Item2.Directory;
                        Password = loginResponse.Item1;
                        Session = loginResponse.Item2;

                    Console.WriteLine ("Welcome Session Path" + loginResponse.Item2.Directory.ConvertToString());


                        List<string> WelcomePhrase = new List<string>
            {
                "Hello there",
                "It's you again",
                "GLORY TO MANKIND",
                "Welcome Back"
                        };

                        var randomPhrase = WelcomePhrase[new Random().Next(WelcomePhrase.Count)];
                        WelcomeText.Text = $"{randomPhrase}, {usernameStr}.";

                        InitializeMainSoftware();
                    }
                
                catch (Exception ex)
                {
                    ErrorLogin.Text = "Login Failed: " + ex.Message;

                    PasswordLogin.Content = "Enter Password";
                    PasswordLogin.IsEnabled = true;
                    CancelLogin.IsEnabled = true;
                }



            };



            var textTimer = new DispatcherTimer();
            var dotTimer = new DispatcherTimer();

            Welcome.IsVisibleChanged += (sender, e) =>
            {
                if (Welcome.IsVisible == true)
                {

                    List<string> FunTexts = new List<string>
                    {
                        "Brewing Mira's Coffee",
                        "Asking Caelis For Server Help",
                        "Checking Computer Specs",
                        "Making An Order With Willow",
                        "Waking Up Zeke",
                        "Getting Info From CATAPHRACT",
                        "Checking In Lunch With Shepard",
                        "Asking Blank To Get Me A Milkshake",
                        "Forcing Acilia To Fix Some Hardware"
                    };

                    var randoGen = new Random();

                    void TextTimer_Tick(object sender, EventArgs e)
                    {
                        int randomIndex = randoGen.Next(FunTexts.Count);
                        FunText.Text = FunTexts[randomIndex];
                    }

                    void DotTimer_Tick(object sender, EventArgs e)
                    {
                        FunText.Text = FunText.Text + ".";
                    }

                    textTimer.Interval = TimeSpan.FromSeconds(4); // change every 3 seconds                    
                    textTimer.Tick += TextTimer_Tick;
                    textTimer.Start();


                    dotTimer.Interval = TimeSpan.FromSeconds(1); // change every second                    
                    dotTimer.Tick += DotTimer_Tick;
                    dotTimer.Start();

                }

                else
                {
                    textTimer.Stop();
                    dotTimer.Stop();
                    FunText.Text = "";
                }
            };


            CancelLogin.Click += (s, e) =>
            {
                Accounts.Visibility = Visibility.Visible;
                SignIn.Visibility = Visibility.Collapsed;
                ResetPage();
            };


        }



        public void ResetPage()
        {
            CreateAccTabControl.Items.Clear();
            Username = default;
            Password = default;
            Directory = default;
        }

        public void GoToSignUp(object sender, EventArgs e)
        {
            foreach (var child in childrenElements)
            {
                if (child is FrameworkElement fe && fe.Name != "Accounts" && fe.Name != "BG")
                {
                    child.Visibility = Visibility.Collapsed;
                }
                else
                {
                    child.Visibility = Visibility.Visible;
                }
            }

            Setup_User_Creation_Suite();

        }

        public async Task PopulateUsersPage()
        {
            UsersGrid.Children.Clear();


            var baseDir = AppContext.BaseDirectory;
            var dbDesignDir = Path.Combine(baseDir, "DatabaseDesignerData");

            try

            {

                var JsonList = new List<(string, string, string)>();

                if (!System.IO.Directory.Exists(dbDesignDir))
                    throw new Exception("Directory not found.");

                var subDirs = System.IO.Directory.GetDirectories(dbDesignDir);

                foreach (var dir in subDirs)
                {
                    var jsonPath = Path.Combine(dir, "PublicUserData.json");
                    if (!File.Exists(jsonPath))
                        continue;

                    try
                    {
                        var jsonContent = await File.ReadAllTextAsync(jsonPath);
                        var FinalObj = JsonObject.Parse(jsonContent);

                        var username = FinalObj["Username"]?.ToString();
                        var pfp = FinalObj["PFP"]?.ToString();

                        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(pfp))
                        {
                            JsonList.Add((
                                username,
                                pfp,
                                dir
                            ));
                        }
                    }
                    catch
                    {
                        // Skip silently
                    }
                }




            
                UsersGrid.Children.Clear();

                var CreateAccBtn = CreateUserTile("Create Account", "/Database_Designer;component/assets/images/blacklogo.png");
                UsersGrid.Children.Add(CreateAccBtn.Item1);

                CreateAccBtn.Item2.Click += async (s, e) =>
                {
                    await Setup_User_Creation_Suite();
                    Accounts.Visibility = Visibility.Collapsed;
                    SignUp.Visibility = Visibility.Visible;
                };


                foreach (var account in JsonList)
                {
                    var newPFP = CreateUserTile(account.Item1, $"/Database_Designer;component/{account.Item2}");
                    UsersGrid.Children.Add(newPFP.Item1);
                    newPFP.Item2.Click += async (s, e) =>
                    {
                        Username = account.Item1.ToSecureData();
                        PFP = account.Item2.ToSecureData();
                        Directory = account.Item3.ToString().ToSecureData();

                        Accounts.Visibility = Visibility.Collapsed;
                        SignIn.Visibility = Visibility.Visible;

                        LoginUsername.Text = Username.ConvertToString();

                        PFPLogin.Source = new BitmapImage(new Uri(PFP.ConvertToString(), uriKind: UriKind.Relative));

                    };

                }


            }

            catch
            {
                var CreateAccBtn = CreateUserTile("Create Account", "/Database_Designer;component/assets/images/blacklogo.png");
                UsersGrid.Children.Add(CreateAccBtn.Item1);

                CreateAccBtn.Item2.Click += (s, e) =>
                {
                    GoToSignUp(s, e);
                    _ = Setup_User_Creation_Suite();
                };
            }



        }

        public (Grid, Button) CreateUserTile(string Name, string ImagePath)
        {
            // Create the main grid
            var grid = new Grid
            {
                Width = 120,
                Height = 160,
                Background = new SolidColorBrush(Color.FromArgb(0x00, 0xD4, 0xD4, 0xD4)),
                Margin = new Thickness(2, 5, 2, 5)
            };

            // Create the button
            var createAccBtn = new Button
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xD8, 0xC0)),
                Name = "CreateAccBtn"
            };
            grid.Children.Add(createAccBtn);

            // Create the image
            var image = new Image
            {
                Source = new BitmapImage(new Uri(ImagePath, UriKind.Relative)),
                Height = 106,
                Width = 108,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                IsEnabled = false
            };
            grid.Children.Add(image);

            // Create the text block
            var textBlock = new TextBlock
            {
                Text = Name,
                FontSize = 16,
                Height = 42,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 4),
                IsEnabled = false,
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black)
            };
            grid.Children.Add(textBlock);

            return (grid, createAccBtn);
        }

        //This is the function that will be called when the user clicks the "Accounts" button
        private async Task<string> Check_DB_Setup()
        {
            var baseDir = AppContext.BaseDirectory;
            var dbDesignDir = Path.Combine(baseDir, "DatabaseDesignerData");

            if (!System.IO.Directory.Exists(dbDesignDir))
            {
                System.IO.Directory.CreateDirectory(dbDesignDir);
            }


            else
            {
                Console.WriteLine("Database directory already exists.");
            }

            return "Good!";
        }


        //This is the function we use to go to the account creation UI. We will go back to the usernames page later.

        //Updated to use methodology from "BasicDatabaseDesigner" file



        // 1. Basics Tab
        public TabItem CreateBasicsTab(TabControl TabControlItem)
        {

            var tab = new TabItem { Header = " Basics", Name = "CreateAccBasicsTab" };
            tab.FontFamily = new System.Windows.Media.FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");
            tab.Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black);

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Who’s Using Database Designer?",
                FontSize = 36,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Width = 835,
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Passwords must be at least 12 characters long and include an uppercase letter, a lowercase letter and a symbol.",
                FontSize = 20,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Username",
                FontSize = 18,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 0),
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black)
            });


            var CreateUsernameInput = new TextBox
            {
                Text = "",
                Padding = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 538,
                Height = 44,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 15,
                Margin = new Thickness(0, 20, 0, 0),
                TextAlignment = TextAlignment.Center,
                Name = "CreateUsernameInput",
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black),
                Background = ThemeManager.ResolveBrush("Theme_TextOnPrimaryColor", Colors.White)
            };

            stack.Children.Add(CreateUsernameInput);

            var CreateUsernameError = new TextBlock
            {
                Text = "Error Text",
                FontSize = 16,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
                Foreground = new SolidColorBrush(Color.FromArgb(250, 255, 0, 0)),
                Name = "CreateRepeatPasswordError",
                Visibility = Visibility.Collapsed
            };
            stack.Children.Add(CreateUsernameError);



            stack.Children.Add(new TextBlock
            {
                Text = "Password",
                FontSize = 18,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 70, 0, 0),
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black)
            });


            var CreatePasswordInput = new PasswordBox
            {
                Padding = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 538,
                Height = 44,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 15,
                Margin = new Thickness(0, 20, 0, 0),
                Name = "CreatePasswordInput",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black),
                Background = ThemeManager.ResolveBrush("Theme_TextOnPrimaryColor", Colors.White)
            };
            stack.Children.Add(CreatePasswordInput);


            stack.Children.Add(new TextBlock
            {
                Text = "Repeat Password",
                FontSize = 18,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 70, 0, 0),
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black)
            });


            var CreateRepeatPasswordInput = new PasswordBox
            {
                Padding = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 538,
                Height = 44,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 15,
                Margin = new Thickness(0, 20, 0, 0),
                Name = "CreateRepeatPasswordInput",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black),
                Background = ThemeManager.ResolveBrush("Theme_TextOnPrimaryColor", Colors.White)
            };
            stack.Children.Add(CreateRepeatPasswordInput);

            var CreatePasswordError = new TextBlock
            {
                Text = "Error Text",
                FontSize = 16,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, -40),
                Foreground = new SolidColorBrush(Color.FromArgb(250, 255, 0, 0)),
                Name = "CreateRepeatPasswordError",
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap
            };

            stack.Children.Add(CreatePasswordError);


            var buttonPanel = new StackPanel
            {
                Height = 52,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 100, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), // Transparent
                Width = 593
            };
            stack.Children.Add(buttonPanel);






            var CreateAccCancel = new Button
            {
                Content = "Cancel",
                Height = 46,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454138")),
                Width = 287,
                Name = "CreateAccCancel"
            };
            buttonPanel.Children.Add(CreateAccCancel);

            buttonPanel.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = 18,
                StrokeThickness = 1,
                Height = 32,
                Stroke = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), // Transparent
                Fill = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)) // Transparent
            });


            var ContinueButton = new Button
            {
                Content = "Continue",
                Height = 46,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454138")),
                Width = 287,
                Name = "CreateAccNext1"
            };
            buttonPanel.Children.Add(ContinueButton);




            tab.Content = stack;


            ContinueButton.Click += async (s, e) =>
            {

                #region Username Check

                CreateUsernameError.Visibility = Visibility.Collapsed;


                if (string.IsNullOrWhiteSpace(CreateUsernameInput.Text))
                {
                    CreateUsernameError.Visibility = Visibility.Visible;
                    CreateUsernameError.Text = "Please enter a username!";
                    return;
                }

                var userManagement = new DataHandler.AccountsWithSessions();

                var baseDir = AppContext.BaseDirectory;
                var dbDesignDir = Path.Combine(baseDir.ToString(), "DatabaseDesignerData");

                var result = await JSONDataHandler.LoadJsonFile("Users", dbDesignDir);

                var names = new List<string>();

                try
                {
                    List<AccountData>? UserList = (List<AccountData>)await JSONDataHandler.GetVariable<List<AccountData>>(result, "AccountsList", SecuritySettings.PublicKey);

                    foreach (var item in UserList)
                    {
                        names.Add(item.Username);
                    }

                }


                catch (Exception ex)
                {

                }


                var isUsernameInList = names.Contains(CreateUsernameInput.Text);

                if (isUsernameInList)
                {
                    CreateUsernameError.Visibility = Visibility.Visible;
                    CreateUsernameError.Text = "This username is already taken!";
                    return;
                }

                #endregion


                #region Password Check

                //Passwords must be 12+ characters, have an uppercase, lowercase and symbol 
                CreatePasswordError.Visibility = Visibility.Collapsed;

                //Disable buttons until return; later

                var enteredPass = CreatePasswordInput.Password;

                if (string.IsNullOrWhiteSpace(enteredPass))
                {
                    CreatePasswordError.Visibility = Visibility.Visible;
                    CreatePasswordError.Text = "Please enter a password!";
                    return;
                }

                if (enteredPass.Length < 12)
                {
                    CreatePasswordError.Visibility = Visibility.Visible;
                    CreatePasswordError.Text = "Password must be at least 12 characters!";
                    return;
                }

                if (!enteredPass.Any(char.IsUpper))
                {
                    CreatePasswordError.Visibility = Visibility.Visible;
                    CreatePasswordError.Text = "Password must contain at least one uppercase letter!";
                    return;
                }

                if (!enteredPass.Any(char.IsLower))
                {
                    CreatePasswordError.Visibility = Visibility.Visible;
                    CreatePasswordError.Text = "Password must contain at least one lowercase letter!";
                    return;
                }

                if (!enteredPass.Any(ch => "!@#$%^&*()_+-=[]{}|;':\",.<>?/`~".Contains(ch)))
                {
                    CreatePasswordError.Visibility = Visibility.Visible;
                    CreatePasswordError.Text = "Password must contain at least one special character!";
                    return;
                }

                var passDoubleCheck = CreateRepeatPasswordInput.Password;

                //Passwords must match

                if (string.IsNullOrWhiteSpace(passDoubleCheck))
                {
                    CreateRepeatPasswordInput.Visibility = Visibility.Visible;
                    CreatePasswordError.Visibility = Visibility.Visible;
                    CreatePasswordError.Text = "Please re-enter your password!";
                    return;
                }

                if (enteredPass != passDoubleCheck)
                {
                    CreatePasswordError.Visibility = Visibility.Visible;
                    CreatePasswordError.Text = "Passwords do not match!\n\nNote: Navigating away from this tab clears your password for security — even if dots remain visible. Please re-type both passwords.";
                    return;
                }



                #endregion



                Username = CreateUsernameInput.Text.ToSecureData();
                Password = passDoubleCheck.ToSecureData();
                TabControlItem.SelectedIndex = 1;




            };

            CreateAccCancel.Click += (s, e) =>
            {

                ResetPage();

                Accounts.Visibility = Visibility.Visible;
                SignUp.Visibility = Visibility.Collapsed;


            };







            return tab;
        }

        // 2. Profile Image Tab
        public TabItem CreateProfileImageTab(TabControl TabControlItem)
        {
            var tab = new TabItem { Header = "Profile Image", Name = "CreateAccPFPTab", FontSize = 14 };
            tab.FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");
            tab.Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Select a PFP",
                FontSize = 36,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Width = 835
            });

            var canvas = new Canvas { Height = 590, Margin = new Thickness(0, 20, 0, 20), Background = new SolidColorBrush(Color.FromArgb(0, 230, 230, 230)) };
            var scroll = new ScrollViewer
            {
                Width = 784,
                Height = 600,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Visible
            };
            var wrap = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromArgb(0, 212, 212, 212)),
                Width = 764,
                Name = "PFPOptions"
            };



            string basePath = "assets/images/PFPs/";

            List<string> images = new List<string>
            {
                "Ellipse 1.png",
                "Ellipse 2.png",
                "Ellipse 3.png",
                "Ellipse 4.png",
                "Ellipse 5.png",
                "Ellipse 6.png",
                "Ellipse 7.png",
                "Ellipse 8.png",
                "Ellipse 9.png",
                "Ellipse 10.png",
                "Ellipse 11.png",
                "Ellipse 12.png",
                "Ellipse 13.png",
                "Ellipse 14.png",
                "Ellipse 15.png",
                "Ellipse 17.png",
                "Ellipse 18.png",
                "image.png"
            };


            foreach (var img in images)
            {
                var PFPOption = new Grid
                {
                    Width = 150,
                    Height = 150,
                    Name = "PFPOption"
                };

                // Create the button
                var CreateAccPFPChoice = new Button
                {
                    Width = 150,
                    Height = 150,
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xD8, 0xC0)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Name = "CreateAccPFPChoice"
                };

                // Create the image
                var image = new Image
                {
                    Source = new BitmapImage(new Uri($"{basePath}{img}", UriKind.Relative)),
                    Width = 150,
                    IsHitTestVisible = false
                };

                // Add the elements to the Grid
                PFPOption.Children.Add(CreateAccPFPChoice);
                PFPOption.Children.Add(image);

                CreateAccPFPChoice.Click += (s, e) =>
                {

                    PFP = $"{basePath}{img}".ToSecureData();

                };
                wrap.Children.Add(PFPOption);
            }






            scroll.Content = wrap;
            Canvas.SetLeft(scroll, 0);
            Canvas.SetTop(scroll, 0);
            canvas.Children.Add(scroll);
            stack.Children.Add(canvas);


            var buttonPanel = new StackPanel
            {
                Height = 52,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 20, 0, 40),
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                Width = 518
            };

            var GoBackButton = new Button
            {
                Content = "Go Back",
                Height = 46,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454138")),
                Width = 160,
                Name = "CreateAccBack2"
            };

            buttonPanel.Children.Add(GoBackButton);


            buttonPanel.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = 18,
                StrokeThickness = 1,
                Height = 32,
                Stroke = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                Fill = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)) // Transparent
            });

            //var SelectGalleryButton = new Button
            //{
            //   Content = "Select From Gallery",
            //  Height = 46,
            // Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454138")),
            //Width = 160,
            //Name = "CreateAccSelectFromGallery"
            //};

            //buttonPanel.Children.Add(SelectGalleryButton);


            buttonPanel.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = 18,
                StrokeThickness = 1,
                Height = 32,
                Stroke = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), // Transparent
                Fill = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)) // Transparent
            });

            var ContinueButton = new Button
            {
                Content = "Continue",
                Height = 46,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454138")),
                Width = 160,
                Name = "CreateAccNext2"
            };
            buttonPanel.Children.Add(ContinueButton);
            stack.Children.Add(buttonPanel);

            ContinueButton.Click += (s, e) =>
            {
                TabControlItem.SelectedIndex = 2;
            };

            GoBackButton.Click += (s, e) =>
            {
                TabControlItem.SelectedIndex = 0; // Go back to Basics Tab
            };



            tab.Content = stack;
            return tab;
        }

        // 3. Account Overview Tab
        public TabItem CreateAccountOverviewTab(TabControl TabControlItem)
        {
            var tab = new TabItem { Header = "Account Overview", Name = "CreateAccOverview" };
            tab.FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");
            tab.Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Account Overview",
                FontSize = 36,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Width = 835,
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Review your account details below.",
                FontSize = 20,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Profile Image",
                FontSize = 18,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var image = new Image
            {
                Source = new BitmapImage(new Uri("assets/images/blacklogo.png", UriKind.Relative)),
                Width = 150,
                Height = 150,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Name = "CreateAccSelectedPFPConfirm"
            };

            stack.Children.Add(image);

            var UsernameBlock = new TextBlock
            {
                Text = "Username: User123",
                FontSize = 18,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 0),
                Name = "CreateAccUsername",
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black)
            };

            stack.Children.Add(UsernameBlock);

            var PasswordBlock = new TextBlock
            {
                Text = "Password: ********",
                FontSize = 18,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Name = "CreateAccPasswordConfirm",
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black)
            };

            stack.Children.Add(PasswordBlock);

            var buttonPanel = new StackPanel
            {
                Height = 52,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 100, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), // Transparent
                Width = 593
            };
            var GoBack = new Button
            {
                Content = "Go Back",
                Height = 46,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454138")),
                Width = 287,
                Name = "CreateAccBack3"
            };

            buttonPanel.Children.Add(GoBack);

            buttonPanel.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = 18,
                StrokeThickness = 1,
                Height = 32,
                Stroke = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), // Transparent
                Fill = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)) // Transparent
            });

            var Confirm = new Button
            {
                Content = "Confirm",
                Height = 46,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454138")),
                Width = 287,
                Name = "CreateAccConfirm3",
                IsEnabled = false
            };

            buttonPanel.Children.Add(Confirm);

            stack.Children.Add(buttonPanel);

            TabControlItem.SelectionChanged += (s, e) =>
            {
                if (TabControlItem.SelectedIndex == 2) // Account Overview Tab
                {
                    try
                    {
                        if (PFP == null || string.IsNullOrEmpty(PFP.ConvertToString()))
                        {
                            PFP = "assets/images/PFPs/Ellipse 1.png".ToSecureData(); // Default PFP
                        }
                    }
                    catch (ArgumentNullException ex)
                    {
                        // Handle null argument exception
                        Console.WriteLine($"ArgumentNullException caught: {ex.Message}");
                        // Fallback: assign a safe default value
                        PFP = "assets/images/PFPs/Ellipse 1.png".ToSecureData();
                    }
                    catch (Exception ex)
                    {
                        // Handle any other exceptions
                        Console.WriteLine($"Unexpected error: {ex.Message}");
                    }

                    image.Source = new BitmapImage(new Uri(PFP.ConvertToString(), uriKind: UriKind.Relative));

                    var usernameInput = (TextBox)TabControlItem.Items.OfType<TabItem>()
                        .SelectMany(t => ((StackPanel)t.Content).Children.OfType<TextBox>())
                        .FirstOrDefault(tb => tb.Name == "CreateUsernameInput");
                    var passwordInput = (PasswordBox)TabControlItem.Items.OfType<TabItem>()
                        .SelectMany(t => ((StackPanel)t.Content).Children.OfType<PasswordBox>())
                        .FirstOrDefault(pb => pb.Name == "CreatePasswordInput");
                    if (usernameInput != null && passwordInput != null)
                    {
                        try
                        {
                            if (Username == null || string.IsNullOrEmpty(Username.ConvertToString()))
                            {
                                Username = "Default".ToSecureData(); // Default username
                            }
                        }
                        catch (ArgumentNullException ex)
                        {
                            Console.WriteLine($"ArgumentNullException caught: {ex.Message}");
                            // Fallback default
                            Username = "Default".ToSecureData();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Unexpected error: {ex.Message}");
                        }

                        UsernameBlock.Text = $"Username: {Username.ConvertToString()}";
                        try
                        {
                            if (Password == null || string.IsNullOrEmpty(Password.ConvertToString()))
                            {
                                Password = "Default".ToSecureData(); // Default password
                            }
                        }
                        catch (ArgumentNullException ex)
                        {
                            Console.WriteLine($"ArgumentNullException caught: {ex.Message}");
                            // Assign fallback default password
                            Password = "Default".ToSecureData();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Unexpected error: {ex.Message}");
                            // Assign fallback default password
                            Password = "Default".ToSecureData();
                        }

                        PasswordBlock.Text = $"Password: {new string('*', Password.ConvertToString().Length)}";
                    }

                    if (Username != null && !string.IsNullOrEmpty(Username.ConvertToString()) && Username.ConvertToString() != "Default" && Password != null
                        && !string.IsNullOrEmpty(Password.ConvertToString()) & Password.ConvertToString() != "Default")
                    {
                        Confirm.IsEnabled = true;
                    }
                    else
                    {
                        Confirm.IsEnabled = false;
                    }


                }

            };

            GoBack.Click += (s, e) =>
            {
                TabControlItem.SelectedIndex = 1; // Go back to Profile Image Tab
            };

            Confirm.Click += async (s, e) =>
            {

                var baseDir = AppContext.BaseDirectory;
                var dbDesignDir = Path.Combine(baseDir, "DatabaseDesignerData");


                #region Create User

                var ACS = new Pariah_Cybersecurity.DataHandler.AccountsWithSessions();

                var recoveryKey = await ACS.CreateUser(Username.ConvertToString(), Password, dbDesignDir);


                #endregion


                #region Create User Folder


                //Create Folder For User
                string userFolder = Path.Combine(dbDesignDir, Username.ConvertToString());


                System.IO.Directory.CreateDirectory(userFolder);



                #endregion


                #region Create Public User Data File

                var publicUserData = new JsonObject
                {
                    ["Username"] = Username.ConvertToString(),
                    ["PFP"] = PFP.ConvertToString(), //Img Path
                    ["Directory"] = dbDesignDir
                };


                await JSONDataHandler.CreateJsonFile("PublicUserData", userFolder, publicUserData);

                #endregion


                #region Create Projects Folder
                string ProjectsFolder = Path.Combine(userFolder, "Projects");
                System.IO.Directory.CreateDirectory(ProjectsFolder);
                #endregion

                #region Create Subfolders for Exports/Templates

                // Row Template Packs (individual table templates)
                string rowTemplatesFolder = Path.Combine(userFolder, "Row Templates");
                System.IO.Directory.CreateDirectory(rowTemplatesFolder);


                // Project Template Packs (full encrypted session templates)
                string projectTemplatesFolder = Path.Combine(userFolder, "Project Templates");
                System.IO.Directory.CreateDirectory(projectTemplatesFolder);

                //Put templates here;
                DefaultTemplates.Install(userFolder);



                #endregion

                var KeyTab = CreateRecoveryKeyTab(TabControlItem, recoveryKey);
                TabControlItem.Items.Add(KeyTab);
                TabControlItem.SelectedIndex = 3;


            };

            tab.Content = stack;
            return tab;
        }

        // 4. Recovery Key Tab
        public TabItem CreateRecoveryKeyTab(TabControl TabControlItem, SecureData RecoveryKey)
        {
            var tab = new TabItem { Header = "Recovery Key", Name = "CreateAccRecoveryKey" };
            tab.FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");
            tab.Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Recovery Key",
                FontSize = 36,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Width = 835,
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "This is your recovery key. Save it now; without it, you won't be able to recover this account.",
                FontSize = 20,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Width = 835,
                Name = "RecoveryKeyText",
                IsEnabled = false,
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black)
            });


            var recoveryKeyTextBlock = new TextBlock
            {
                Text = RecoveryKey.ConvertToString(),
                FontSize = 18,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Width = 760,
                Name = "RecoveryKeyDisplay",
                Foreground = ThemeManager.ResolveBrush("Theme_BackgroundColor", Colors.Black),
                Visibility = Visibility.Visible,
                TextWrapping = TextWrapping.Wrap
            };

            stack.Children.Add(recoveryKeyTextBlock);

            var finalButton = new Button
            {
                Content = "Okay",
                Height = 46,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454138")),
                Width = 150,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 0),
                Name = "CreateAccOkay",

            };

            var copyButton = new Button
            {
                Content = "Copy To Clipboard (CLICK THIS FIRST)",
                Height = 46,
                Width = 220,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454138")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 0),
                Name = "CreateAccOkay",
            };

            // Attach Click event to copy "Yes" to clipboard
            copyButton.Click += (s, e) =>
            {
                Clipboard.SetTextAsync(RecoveryKey.ConvertToString());
            };


            stack.Children.Add(finalButton);
            stack.Children.Add(copyButton);


            finalButton.Click += (s, e) =>
            {
                //Reset values

                ResetPage();

                Accounts.Visibility = Visibility.Visible;
                SignUp.Visibility = Visibility.Collapsed;

            };

            tab.Content = stack;

            return tab;
        }








        private async Task Setup_User_Creation_Suite()
        {
            await Check_DB_Setup();

            // Always start fresh when entering signup
            CreateAccTabControl.Items.Clear();

            var Tab1 = CreateBasicsTab(CreateAccTabControl);
            var Tab2 = CreateProfileImageTab(CreateAccTabControl);
            var Tab3 = CreateAccountOverviewTab(CreateAccTabControl);

            CreateAccTabControl.Items.Add(Tab1);
            CreateAccTabControl.Items.Add(Tab2);
            CreateAccTabControl.Items.Add(Tab3);

            CreateAccTabControl.TabIndex = 0;


            //Username Checks
            Username = default;
            //Password Checks
            Password = default;

            PFP = default;




        }










    }
}