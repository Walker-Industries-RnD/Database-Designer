using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Database_Designer
{
    public partial class APIEditorWindow : Page
    {
        private readonly APIData _data;
        private readonly MainPage _host;
        public event Action<APIEditorWindow> CloseRequested;
        public event Action<APIEditorWindow> SaveRequested;
        public UIWindowEntry WindowInfo { get; private set; }

        public APIData Data => _data;

        public APIEditorWindow(MainPage host = null)
        {
            _host = host;
            _data = new APIData();
            _data.SeedDefaults();
            InitializeComponent();
            NavigateToPage1();
        }

        public void LoadFromJson(string json)
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<APIData>(json);
                if (loaded != null)
                {
                    _data.Modules = loaded.Modules;
                    _data.SelectedModuleIndex = loaded.SelectedModuleIndex;
                    _data.SelectedEndpointIndex = loaded.SelectedEndpointIndex;
                }
            }
            catch { }
            RefreshCurrentPage();
        }

        private void RefreshCurrentPage()
        {
            if (APIFrame.Content is APIPage1Modules page1)
                page1.OnNavigatedTo();
            else if (APIFrame.Content is APIPage2Endpoints page2)
                page2.OnNavigatedTo();
            else if (APIFrame.Content is APIPage3Functions page3)
                page3.OnNavigatedTo();
        }

        public string SaveToJson()
        {
            return JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this);
        }

        public static APIData CreateAndLoad(string json)
        {
            var data = new APIData();
            data.SeedDefaults();
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<APIData>(json);
                    if (loaded != null)
                    {
                        data.Modules = loaded.Modules;
                        data.SelectedModuleIndex = loaded.SelectedModuleIndex;
                        data.SelectedEndpointIndex = loaded.SelectedEndpointIndex;
                    }
                }
                catch { }
            }
            return data;
        }

        private void NavigateToPage1()
        {
            APIPage1Modules page = new APIPage1Modules(_data, _host);
            page.NavigateToEndpoints += OnNavigateToEndpoints;
            page.CloseRequested += () => CloseRequested?.Invoke(this);
            page.SaveRequested += () => SaveRequested?.Invoke(this);
            APIFrame.Content = page;
            page.OnNavigatedTo();
        }

        private void OnNavigateToEndpoints(APIData data)
        {
            APIPage2Endpoints page = new APIPage2Endpoints(data, _host);
            page.BackToModules += OnBackToModules;
            page.NavigateToFunctions += OnNavigateToFunctions;
            page.CloseRequested += () => CloseRequested?.Invoke(this);
            page.SaveRequested += () => SaveRequested?.Invoke(this);
            APIFrame.Content = page;
            page.OnNavigatedTo();
        }

        private void OnBackToModules()
        {
            NavigateToPage1();
        }

        private void OnNavigateToFunctions(APIData data)
        {
            APIPage3Functions page = new APIPage3Functions(data, _host);
            page.BackToEndpoints += OnBackToEndpoints;
            page.CloseRequested += () => CloseRequested?.Invoke(this);
            page.SaveRequested += () => SaveRequested?.Invoke(this);
            APIFrame.Content = page;
            page.OnNavigatedTo();
        }

        private void OnBackToEndpoints()
        {
            OnNavigateToEndpoints(_data);
        }
    }
}