using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    public partial class FunctionCreator : Page
    {
        public class FunctionModule
        {
            public string Name { get; set; } = "Untitled Module";
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
            public List<Endpoint> Endpoints { get; } = new();
            public int TotalFunctions => Endpoints.Sum(e => e.Functions.Count);
        }
        public class Endpoint
        {
            public string Name { get; set; } = "Endpoint";
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
            public List<FunctionEntry> Functions { get; } = new();
        }
        public class FunctionEntry
        {
            public string Name { get; set; } = "Function Name";
            public string Verb { get; set; } = "GET"; // GET / POST / PUT / DELETE
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
        }

        public List<FunctionModule> Modules { get; } = new();
        private FunctionModule _selectedModule;
        private Endpoint _selectedEndpoint;

        private static readonly Dictionary<string, Color> VerbColors = new()
        {
            ["GET"]    = Color.FromRgb(0x39, 0xA9, 0x5F),
            ["POST"]   = Color.FromRgb(0x7B, 0x3C, 0xFF),
            ["PUT"]    = Color.FromRgb(0xFF, 0x9F, 0x33),
            ["DELETE"] = Color.FromRgb(0xFF, 0x4C, 0x4C),
        };

        // Optional host — set when this page was opened from inside DBD's
        // MainPage. Lets function rows launch the underlying NodeWalker graph
        // editor against the project's per-function session file.
        public MainPage HostPage { get; }
        public FunctionCreator(MainPage host) : this() { HostPage = host; }

        public FunctionCreator()
        {
            InitializeComponent();
            SeedDefaults();
            CreateNewModuleBtn.Click   += (s, e) => CreateModuleFromInput();
            CreateNewEndpointBtn.Click += (s, e) => CreateEndpointFromInput();
            CreateNewFunctionBtn.Click += (s, e) => CreateFunctionFromInput();
            RebuildAll();
        }

        private void SeedDefaults()
        {
            string[] mods = { "Auth", "Users", "Marketplace", "Admin" };
            foreach (var n in mods) Modules.Add(new FunctionModule { Name = n });
            _selectedModule = Modules.FirstOrDefault();
        }

        private void RebuildAll()
        {
            RebuildModuleCards();
            RebuildSelectedModulePanel();
            RebuildFunctionEditor();
        }

        private void RebuildModuleCards()
        {
            ModuleCardsHost.Children.Clear();
            foreach (var m in Modules)
            {
                var captured = m;
                var card = BuildSummaryCard(
                    m.Name, m.Description, m.TotalFunctions,
                    isSelected: m == _selectedModule,
                    titleCommit: v => { captured.Name = v; RebuildAll(); },
                    descCommit:  v => { captured.Description = v; });
                card.MouseLeftButtonUp += (s, e) =>
                {
                    _selectedModule   = captured;
                    _selectedEndpoint = captured.Endpoints.FirstOrDefault();
                    RebuildAll();
                };
                var stack = ((StackPanel)((Border)card.Child).Child);
                foreach (var f in m.Endpoints.SelectMany(e => e.Functions).Take(8))
                    stack.Children.Add(BuildFunctionChip(f, narrow: true));
                if (m.TotalFunctions > 8)
                    stack.Children.Add(new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 4, 0, 0),
                        Child = new TextBlock
                        {
                            Text = $"+{m.TotalFunctions - 8} More",
                            Foreground = Brushes.White, FontSize = 10
                        }
                    });
                ModuleCardsHost.Children.Add(card);
            }
        }

        private Border BuildSummaryCard(string title, string desc, int count, bool isSelected,
            Action<string> titleCommit = null, Action<string> descCommit = null)
        {
            var inner = new StackPanel();
            var headerGrid = new Grid();
            headerGrid.Children.Add(new Border
            {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                HorizontalAlignment = HorizontalAlignment.Left
            });
            headerGrid.Children.Add(new TextBlock
            {
                Text = $"{Math.Min(count, 999)} Functions",
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
                FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right
            });
            inner.Children.Add(headerGrid);

            var titleEl = EditableTextHelpers.EditableText(
                title, titleCommit, fontSize: 14, weight: FontWeights.Bold);
            ((Grid)titleEl).Margin = new Thickness(0, 12, 0, 4);
            inner.Children.Add(titleEl);

            var descEl = EditableTextHelpers.EditableText(
                desc, descCommit, fontSize: 10,
                foreground: Color.FromRgb(0x88, 0x88, 0x88),
                wrap: true, maxWidth: 170);
            ((Grid)descEl).Margin = new Thickness(0, 0, 0, 8);
            inner.Children.Add(descEl);
            return new Border
            {
                Width = 188, Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(12),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0x7B, 0x3C, 0xFF))
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                Child = new Border { Background = Brushes.Transparent, Child = inner }
            };
        }

        private Border BuildFunctionChip(FunctionEntry f, bool narrow)
        {
            var color = VerbColors.TryGetValue(f.Verb, out var c)
                ? c : Color.FromRgb(0x39, 0xA9, 0x5F);
            return new Border
            {
                Background = new SolidColorBrush(color),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 2, 0, 2),
                CornerRadius = new CornerRadius(2),
                Child = new TextBlock
                {
                    Text = $"{f.Verb} {f.Name}",
                    Foreground = Brushes.White, FontSize = narrow ? 9 : 11,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            };
        }

        private void RebuildSelectedModulePanel()
        {
            EndpointCardsHost.Children.Clear();
            SelectedModuleFnChips.Children.Clear();
            if (_selectedModule == null)
            {
                SelectedModuleName.Text = "No module selected";
                SelectedModuleFnCount.Text = "0 Functions";
                return;
            }
            SelectedModuleName.Text = _selectedModule.Name;
            SelectedModuleDesc.Text = _selectedModule.Description;
            SelectedModuleFnCount.Text = $"{_selectedModule.TotalFunctions} Functions";

            foreach (var f in _selectedModule.Endpoints.SelectMany(e => e.Functions).Take(12))
                SelectedModuleFnChips.Children.Add(BuildFunctionChip(f, narrow: true));

            foreach (var ep in _selectedModule.Endpoints)
            {
                var captured = ep;
                var card = BuildEndpointCard(ep, isSelected: ep == _selectedEndpoint);
                card.MouseLeftButtonUp += (s, e) =>
                {
                    _selectedEndpoint = captured;
                    RebuildFunctionEditor();
                };
                EndpointCardsHost.Children.Add(card);
            }
        }

        private Border BuildEndpointCard(Endpoint ep, bool isSelected)
        {
            var stack = new StackPanel();
            var header = new Grid();
            header.Children.Add(new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                HorizontalAlignment = HorizontalAlignment.Left
            });
            header.Children.Add(new TextBlock
            {
                Text = $"{Math.Min(ep.Functions.Count, 999)} Functions",
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
                FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right
            });
            stack.Children.Add(header);

            var capturedEp = ep;
            var titleEl = EditableTextHelpers.EditableText(
                ep.Name, v => { capturedEp.Name = v; RebuildAll(); },
                fontSize: 13, weight: FontWeights.Bold);
            ((Grid)titleEl).Margin = new Thickness(0, 8, 0, 4);
            stack.Children.Add(titleEl);

            var descEl = EditableTextHelpers.EditableText(
                ep.Description, v => { capturedEp.Description = v; },
                fontSize: 10, foreground: Color.FromRgb(0x88, 0x88, 0x88),
                wrap: true, maxWidth: 160);
            ((Grid)descEl).Margin = new Thickness(0, 0, 0, 8);
            stack.Children.Add(descEl);

            stack.Children.Add(BuildVerbBar(ep));
            return new Border
            {
                Width = 178, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0x7B, 0x3C, 0xFF))
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand,
                Child = stack
            };
        }

        private StackPanel BuildVerbBar(Endpoint ep)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            if (ep.Functions.Count == 0)
            {
                row.Children.Add(new Border
                {
                    Width = 150, Height = 4,
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A))
                });
                return row;
            }
            foreach (var grp in ep.Functions.GroupBy(f => f.Verb))
            {
                var color = VerbColors.TryGetValue(grp.Key, out var c)
                    ? c : Color.FromRgb(0x39, 0xA9, 0x5F);
                row.Children.Add(new Border
                {
                    Width = 150 * grp.Count() / Math.Max(1, ep.Functions.Count),
                    Height = 4, Background = new SolidColorBrush(color)
                });
            }
            return row;
        }

        private void RebuildFunctionEditor()
        {
            FunctionRowsHost.Items.Clear();
            EditorEndpointUsageBars.Children.Clear();
            VerbLegendHost.Items.Clear();
            foreach (var (verb, desc) in new (string, string)[]
            {
                ("GET",    "Read-only requests. Idempotent and cacheable; should never mutate state."),
                ("POST",   "Create new resources or trigger non-idempotent actions."),
                ("PUT",    "Replace an existing resource in full. Idempotent."),
                ("DELETE", "Remove a resource. Should also be idempotent on the server."),
            })
            {
                VerbLegendHost.Items.Add(BuildVerbLegendTile(verb, desc));
            }

            if (_selectedEndpoint == null)
            {
                EditorEndpointName.Text = "No endpoint selected";
                EditorEndpointFnCount.Text = "0 Functions";
                EditorEndpointDesc.Text = "Pick an endpoint above to edit its functions.";
                return;
            }
            EditorEndpointName.Text = _selectedEndpoint.Name;
            EditorEndpointDesc.Text = _selectedEndpoint.Description;
            EditorEndpointFnCount.Text = $"{_selectedEndpoint.Functions.Count} Functions";
            EditorEndpointUsageBars.Children.Add(BuildVerbBar(_selectedEndpoint));

            foreach (var f in _selectedEndpoint.Functions)
                FunctionRowsHost.Items.Add(BuildFunctionRow(f));
        }

        private Border BuildVerbLegendTile(string verb, string description)
        {
            var color = VerbColors.TryGetValue(verb, out var c)
                ? c : Color.FromRgb(0x39, 0xA9, 0x5F);
            var stack = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            head.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 8, Height = 8, Fill = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center
            });
            head.Children.Add(new TextBlock
            {
                Text = verb, Foreground = Brushes.White,
                FontSize = 11, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(head);
            stack.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontSize = 10, TextWrapping = TextWrapping.Wrap, MaxWidth = 180
            });
            return new Border
            {
                Width = 200, Margin = new Thickness(0, 0, 12, 8),
                Padding = new Thickness(4),
                Child = stack
            };
        }

        private Border BuildFunctionRow(FunctionEntry f)
        {
            var color = VerbColors.TryGetValue(f.Verb, out var c)
                ? c : Color.FromRgb(0x39, 0xA9, 0x5F);
            var captured = f;

            // Layout: [verb pill] [name + description] [Open button]
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var verbPill = new Border
            {
                Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(2),
                Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = f.Verb, Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.Bold }
            };
            Grid.SetColumn(verbPill, 0);
            rowGrid.Children.Add(verbPill);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameEl = EditableTextHelpers.EditableText(
                f.Name, v => { captured.Name = v; }, fontSize: 12, weight: FontWeights.Bold);
            textStack.Children.Add(nameEl);
            var descEl = EditableTextHelpers.EditableText(
                f.Description, v => { captured.Description = v; },
                fontSize: 10, foreground: Color.FromRgb(0x88, 0x88, 0x88), wrap: true);
            ((Grid)descEl).Margin = new Thickness(0, 4, 0, 0);
            textStack.Children.Add(descEl);
            Grid.SetColumn(textStack, 1);
            rowGrid.Children.Add(textStack);

            var openBtn = EditableTextHelpers.OpenInNodeWalkerButton(() =>
            {
                if (HostPage == null) return;
                var slug = $"API_{Slug(_selectedModule?.Name)}_{Slug(_selectedEndpoint?.Name)}_{Slug(captured.Verb)}_{Slug(captured.Name)}";
                HostPage.CreateWindow(
                    () => new NodeWalker.NodeWalkerWindow(HostPage) { Tag = slug },
                    "NodeWalker — " + captured.Name,
                    true, null,
                    Math.Max(900, HostPage.IntroPage.ActualWidth  * 0.85),
                    Math.Max(600, HostPage.IntroPage.ActualHeight * 0.85));
            });
            Grid.SetColumn(openBtn, 2);
            rowGrid.Children.Add(openBtn);

            return new Border
            {
                Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(12),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(0, 0, 0, 2),
                CornerRadius = new CornerRadius(3),
                Child = rowGrid
            };
        }

        private static string Slug(string s) =>
            string.IsNullOrWhiteSpace(s) ? "untitled" :
            new string(s.Where(ch => char.IsLetterOrDigit(ch)).ToArray()).ToLowerInvariant();

        private void CreateModuleFromInput()
        {
            var n = (NewModuleNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            Modules.Add(new FunctionModule { Name = n });
            NewModuleNameBox.Text = "";
            _selectedModule = Modules.Last();
            RebuildAll();
        }
        private void CreateEndpointFromInput()
        {
            if (_selectedModule == null) return;
            var n = (NewEndpointNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            var ep = new Endpoint { Name = n };
            _selectedModule.Endpoints.Add(ep);
            NewEndpointNameBox.Text = "";
            _selectedEndpoint = ep;
            RebuildAll();
        }
        private void CreateFunctionFromInput()
        {
            if (_selectedEndpoint == null) return;
            var n = (NewFunctionNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            _selectedEndpoint.Functions.Add(new FunctionEntry { Name = n });
            NewFunctionNameBox.Text = "";
            RebuildAll();
        }
    }
}
