using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace Database_Designer
{
    public partial class MiraMiniPopup : Page
    {
        private readonly MainPage mainPage;
        public UIWindowEntry WindowInfo { get; private set; }

        private readonly List<(string Text, MiraStates Expression)> dialogs = new();
        private int currentDialogIndex = 0;

        // Removed the extra state parameter — we now only use the list
        public MiraMiniPopup(
            List<(string Text, MiraStates Expression)> miraText,
            MainPage mainPaged)
        {
            InitializeComponent();

            mainPage = mainPaged ?? throw new ArgumentNullException(nameof(mainPaged));

            MiraMiniButton.Click += MiraMiniButton_Click;
            ExitButton.Click += (_, _) => ClosePopup();
            Unloaded += (_, _) => Cleanup();

            SetDialogs(miraText);
        }

        private void SetDialogs(List<(string Text, MiraStates Expression)> textEntries)
        {
            dialogs.Clear();

            if (textEntries == null || textEntries.Count == 0)
            {
                dialogs.Add(("...", MiraStates.Neutral));
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

            MiraMiniText.Text = dialogs[currentDialogIndex].Text;

            // ← This is the important part: change image for current line
            SetMiraImage(dialogs[currentDialogIndex].Expression);

            MiraMiniButton.Content = (currentDialogIndex == dialogs.Count - 1)
                ? "Close"
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
                ClosePopup();
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

        private void SetMiraImage(MiraStates state)
        {
            string fileName = state switch
            {
                MiraStates.Angry => "Angry.png",
                MiraStates.Happy => "Happy.png",
                MiraStates.MyBad => "MyBad.png",
                MiraStates.Nervous => "Nervous.png",
                MiraStates.Neutral => "Neutral.png",
                MiraStates.Ummm => "Ummm.png",
                MiraStates.Error => "Error.png",
                _ => "Neutral.png"
            };

            string path = $"Assets/Images/Mira/{fileName}";

            try
            {
                MiraPopupImg.Source = new BitmapImage(new Uri(path, UriKind.Relative));
            }
            catch
            {
                MiraPopupImg.Source = null;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        public enum MiraStates
        {
            Angry,
            Happy,
            MyBad,
            Nervous,
            Neutral,
            Ummm,
            Error
        }
    }
}