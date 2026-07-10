using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Database_Designer
{
    public partial class RLSEditorWindow : Page
    {
        private readonly RLSData _data;
        private readonly MainPage _host;
        public event Action<RLSEditorWindow> CloseRequested;
        public event Action<RLSEditorWindow> SaveRequested;
        public UIWindowEntry WindowInfo { get; private set; }

        public RLSData Data => _data;

        public RLSEditorWindow(MainPage host = null)
        {
            _host = host;
            _data = new RLSData();
            _data.SeedDefaults();
            InitializeComponent();
            NavigateToPage1();
        }

        public void LoadFromJson(string json)
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<RLSData>(json);
                if (loaded != null)
                {
                    _data.Roles = loaded.Roles;
                    _data.SelectedRoleIndex = loaded.SelectedRoleIndex;
                    _data.SelectedTableIndex = loaded.SelectedTableIndex;
                }
            }
            catch { }
        }

        public string SaveToJson()
        {
            return JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this);
        }

        public static RLSData CreateAndLoad(string json)
        {
            var data = new RLSData();
            data.SeedDefaults();
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<RLSData>(json);
                    if (loaded != null)
                    {
                        data.Roles = loaded.Roles;
                        data.SelectedRoleIndex = loaded.SelectedRoleIndex;
                        data.SelectedTableIndex = loaded.SelectedTableIndex;
                    }
                }
                catch { }
            }
            return data;
        }

        private void NavigateToPage1()
        {
            RLSPage1Roles page = new RLSPage1Roles(_data, _host);
            page.NavigateToTables += OnNavigateToTables;
            page.CloseRequested += () => CloseRequested?.Invoke(this);
            page.SaveRequested += () => SaveRequested?.Invoke(this);
            RLSFrame.Content = page;
            page.OnNavigatedTo();
        }

        private void OnNavigateToTables(RLSData data)
        {
            RLSPage2Tables page = new RLSPage2Tables(data, _host);
            page.BackToRoles += OnBackToRoles;
            page.NavigateToPolicies += OnNavigateToPolicies;
            page.CloseRequested += () => CloseRequested?.Invoke(this);
            page.SaveRequested += () => SaveRequested?.Invoke(this);
            RLSFrame.Content = page;
            page.OnNavigatedTo();
        }

        private void OnBackToRoles()
        {
            NavigateToPage1();
        }

        private void OnNavigateToPolicies(RLSData data)
        {
            RLSPage3Policies page = new RLSPage3Policies(data, _host);
            page.BackToTables += OnBackToTables;
            page.CloseRequested += () => CloseRequested?.Invoke(this);
            page.SaveRequested += () => SaveRequested?.Invoke(this);
            RLSFrame.Content = page;
            page.OnNavigatedTo();
        }

        private void OnBackToTables()
        {
            OnNavigateToTables(_data);
        }
    }
}