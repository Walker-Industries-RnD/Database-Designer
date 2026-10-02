using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    public class PasswordRecoveryPanel : Border
    {
        private readonly string _username;
        private readonly Action _close;
        private readonly StackPanel _form = new StackPanel();
        private readonly TextBox _code;
        private readonly PasswordBox _current;
        private readonly PasswordBox _new1;
        private readonly PasswordBox _new2;
        private readonly TextBlock _status;
        private readonly Button _go;
        private readonly Button _modeCode;
        private readonly Button _modePassword;
        private bool _useCode = true;

        internal static readonly Color Paper = Color.FromRgb(0xF4, 0xF1, 0xEA);
        internal static readonly Color Ink = Color.FromRgb(0x2C, 0x2B, 0x28);
        internal static readonly Color Chrome = Color.FromRgb(0x45, 0x41, 0x38);
        internal static readonly Color Muted = Color.FromRgb(0x75, 0x70, 0x66);
        internal static readonly Color Error = Color.FromRgb(0xC0, 0x28, 0x1E);
        internal static readonly FontFamily Inter = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");

        public PasswordRecoveryPanel(string username, Action close)
        {
            _username = username;
            _close = close;
            Background = new SolidColorBrush(Paper);
            BorderBrush = new SolidColorBrush(Chrome);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(14);
            Padding = new Thickness(36, 28, 36, 28);
            Width = 620;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;

            _form.Children.Add(Text("Reset your password", 28));
            _form.Children.Add(Text("@" + username, 16, Muted, new Thickness(0, 2, 0, 16)));

            var modes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            _modeCode = ModeButton("I have my recovery code");
            _modePassword = ModeButton("I know my password");
            _modeCode.Click += (s, e) => SetMode(true);
            _modePassword.Click += (s, e) => SetMode(false);
            modes.Children.Add(_modeCode);
            modes.Children.Add(_modePassword);
            _form.Children.Add(modes);

            _form.Children.Add(Label("Recovery code"));
            _code = new TextBox { Height = 40, FontSize = 16, FontFamily = Inter, PlaceholderText = "XXXXX-XXXXX-XXXXX-XXXXX-XXXXX" };
            _form.Children.Add(_code);
            _form.Children.Add(Label("Current password"));
            _current = Pw();
            _form.Children.Add(_current);
            _form.Children.Add(Label("New password (at least 8 characters)"));
            _new1 = Pw();
            _form.Children.Add(_new1);
            _form.Children.Add(Label("Confirm new password"));
            _new2 = Pw();
            _form.Children.Add(_new2);

            _status = new TextBlock { FontSize = 13, FontFamily = Inter, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0), MinHeight = 18 };
            _form.Children.Add(_status);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var back = Btn("Back", Color.FromRgb(0x58, 0x56, 0x53), 110);
            back.Click += (s, e) => _close();
            _go = Btn("Reset Password", Chrome, 170);
            _go.Margin = new Thickness(10, 0, 0, 0);
            _go.Click += async (s, e) => await Submit();
            buttons.Children.Add(back);
            buttons.Children.Add(_go);
            _form.Children.Add(buttons);

            _form.Children.Add(Text(
                "Your projects are encrypted with your password, so resetting re-encrypts them; a backup is kept in your Backups folder. " +
                "The old recovery code stops working and you'll get a new one.",
                11, Muted, new Thickness(0, 16, 0, 0)));

            Child = _form;
            SetMode(AccountRecovery.HasVault(AccountRecovery.DataDir, username));
            if (!AccountRecovery.HasVault(AccountRecovery.DataDir, username))
                ShowStatus("This account doesn't have a recovery code yet (codes are created the first time you sign in on this version). " +
                           "If you know your password you can still change it.", false);
        }

        private void SetMode(bool useCode)
        {
            _useCode = useCode;
            _code.Visibility = useCode ? Visibility.Visible : Visibility.Collapsed;
            ((UIElement)_form.Children[_form.Children.IndexOf(_code) - 1]).Visibility = _code.Visibility;
            _current.Visibility = useCode ? Visibility.Collapsed : Visibility.Visible;
            ((UIElement)_form.Children[_form.Children.IndexOf(_current) - 1]).Visibility = _current.Visibility;
            _modeCode.Background = new SolidColorBrush(useCode ? Chrome : Color.FromRgb(0xC9, 0xC4, 0xB8));
            _modePassword.Background = new SolidColorBrush(!useCode ? Chrome : Color.FromRgb(0xC9, 0xC4, 0xB8));
            _go.Content = useCode ? "Reset Password" : "Change Password";
        }

        private async System.Threading.Tasks.Task Submit()
        {
            var invalid = AccountRecovery.ValidateNewPassword(_new1.Password, _new2.Password);
            if (invalid != null) { ShowStatus(invalid, true); return; }
            if (_useCode && AccountRecovery.NormalizeCode(_code.Text).Length == 0) { ShowStatus("Enter your recovery code.", true); return; }
            if (!_useCode && string.IsNullOrEmpty(_current.Password)) { ShowStatus("Enter your current password.", true); return; }

            _go.IsEnabled = false;
            try
            {
                void Progress(string m) => Dispatcher.BeginInvoke(() => ShowStatus(m, false));
                var dataDir = AccountRecovery.DataDir;
                string newCode = _useCode
                    ? await AccountRecovery.ResetWithCode(dataDir, _username, _code.Text, _new1.Password, Progress)
                    : await AccountRecovery.ChangePassword(dataDir, _username, _current.Password, _new1.Password, Progress);
                Child = new RecoveryCodeCard(newCode,
                    "Your password has been changed and your projects were re-encrypted.",
                    "Back to sign in", _close);
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message, true);
                _go.IsEnabled = true;
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            _status.Text = message;
            _status.Foreground = new SolidColorBrush(isError ? Error : Muted);
        }

        internal static TextBlock Text(string text, double size, Color? color = null, Thickness? margin = null) => new TextBlock
        {
            Text = text, FontSize = size, FontFamily = Inter, TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(color ?? Ink), Margin = margin ?? new Thickness(0)
        };

        private static TextBlock Label(string text) => Text(text, 12, Muted, new Thickness(0, 10, 0, 4));

        private static PasswordBox Pw() => new PasswordBox { Height = 40, FontSize = 16, FontFamily = Inter };

        internal static Button Btn(string label, Color bg, double width) => new Button
        {
            Content = label, Width = width, Height = 40, FontSize = 14, FontFamily = Inter,
            Background = new SolidColorBrush(bg), Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };

        private static Button ModeButton(string label)
        {
            var b = Btn(label, Chrome, 220);
            b.Height = 32;
            b.FontSize = 12;
            b.Margin = new Thickness(0, 0, 8, 0);
            return b;
        }
    }

    public class RecoveryCodeCard : StackPanel
    {
        public RecoveryCodeCard(string code, string intro, string doneLabel, Action done)
        {
            Children.Add(PasswordRecoveryPanel.Text("Save your recovery code", 28));
            Children.Add(PasswordRecoveryPanel.Text(intro + " If you ever forget your password, this code is the only way back in, so " +
                "store it somewhere safe (a password manager or on paper). It's shown only once.",
                14, PasswordRecoveryPanel.Muted, new Thickness(0, 8, 0, 18)));

            var box = new TextBox
            {
                Text = code, IsReadOnly = true, FontSize = 24, Height = 56, TextAlignment = TextAlignment.Center,
                FontFamily = new FontFamily("Consolas"), VerticalContentAlignment = VerticalAlignment.Center
            };
            Children.Add(box);

            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var copy = PasswordRecoveryPanel.Btn("Copy", PasswordRecoveryPanel.Chrome, 110);
            copy.Click += (s, e) => { Clipboard.SetTextAsync(code); copy.Content = "Copied ✓"; };
            var ok = PasswordRecoveryPanel.Btn(doneLabel, Color.FromRgb(0x39, 0x7A, 0x4F), 200);
            ok.Margin = new Thickness(10, 0, 0, 0);
            ok.Click += (s, e) => done();
            row.Children.Add(copy);
            row.Children.Add(ok);
            Children.Add(row);
        }
    }

    public class RecoveryCodeWindow : Page
    {
        public UIWindowEntry WindowInfo { get; set; }

        public RecoveryCodeWindow(MainPage host, string code)
        {
            Width = 620;
            Background = new SolidColorBrush(Colors.Transparent);
            Content = new Border
            {
                Background = new SolidColorBrush(PasswordRecoveryPanel.Paper),
                BorderBrush = new SolidColorBrush(PasswordRecoveryPanel.Chrome),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(36, 28, 36, 28),
                Child = new RecoveryCodeCard(code,
                    "We've set up password recovery for your account.",
                    "I've saved it",
                    () => { if (host.IntroPage.Children.Contains(this)) host.IntroPage.Children.Remove(this); })
            };
        }
    }
}
