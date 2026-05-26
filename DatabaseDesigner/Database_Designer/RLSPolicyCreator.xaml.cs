using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    public partial class RLSPolicyCreator : Page
    {
        // Top-level data — kept in-memory so the page stays self-contained;
        // hooks at the bottom let MainPage persist this into the project file.
        public class PolicyRole
        {
            public string Name { get; set; } = "Untitled Role";
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
            public List<TablePolicies> Tables { get; } = new();
            public int TotalPolicies => Tables.Sum(t => t.Policies.Count);
        }
        public class TablePolicies
        {
            public string TableName { get; set; } = "Table Name";
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
            public List<Policy> Policies { get; } = new();
        }
        public class Policy
        {
            public string Name { get; set; } = "Policy Name";
            public string Category { get; set; } = "Base Server"; // Base Server / Profiles / Communication / Economy
            public string Description { get; set; } =
                "User Entered Description Goes Here. Ipsum Lorem Dolor it Cognito Ergo Sum";
        }

        public List<PolicyRole> Roles { get; } = new();
        private PolicyRole _selectedRole;
        private TablePolicies _selectedTable;

        private static readonly Dictionary<string, Color> CategoryColors = new()
        {
            ["Base Server"]   = Color.FromRgb(0xFF, 0x4C, 0x4C),
            ["Profiles"]      = Color.FromRgb(0xFF, 0x4C, 0x4C),
            ["Communication"] = Color.FromRgb(0x7B, 0x3C, 0xFF),
            ["Economy"]       = Color.FromRgb(0x39, 0xA9, 0x5F),
        };

        // Optional host — set when this page was opened from inside DBD's
        // MainPage. Lets policy rows launch the underlying NodeWalker graph
        // editor against the project's per-policy session file.
        public MainPage HostPage { get; }
        public RLSPolicyCreator(MainPage host) : this() { HostPage = host; }

        public RLSPolicyCreator()
        {
            InitializeComponent();
            SeedDefaults();
            CreateNewRoleBtn.Click           += (s, e) => CreateRoleFromInput();
            CreateNewPolicyForTableBtn.Click += (s, e) => CreateTableFromInput();
            CreateNewPolicyBtn.Click         += (s, e) => CreatePolicyFromInput();
            RebuildAll();
        }

        private void SeedDefaults()
        {
            string[] roleNames = { "Standard Users", "Admin Users", "Moderator Users", "Bot Users" };
            foreach (var n in roleNames) Roles.Add(new PolicyRole { Name = n });
            _selectedRole = Roles.FirstOrDefault();
        }

        // ── Top: role cards ───────────────────────────────────────────────
        private void RebuildAll()
        {
            RebuildRoleCards();
            RebuildSelectedRolePanel();
            RebuildPolicyEditor();
        }

        private void RebuildRoleCards()
        {
            RoleCardsHost.Children.Clear();
            foreach (var role in Roles)
            {
                var captured = role;
                var card = BuildSummaryCard(
                    role.Name, role.Description, role.TotalPolicies,
                    isSelected: role == _selectedRole,
                    titleCommit: v => { captured.Name = v; RebuildAll(); },
                    descCommit:  v => { captured.Description = v; });
                card.MouseLeftButtonUp += (s, e) =>
                {
                    _selectedRole  = captured;
                    _selectedTable = captured.Tables.FirstOrDefault();
                    RebuildAll();
                };
                // Inline policy strip
                var stack = ((StackPanel)((Border)card.Child).Child);
                foreach (var p in role.Tables.SelectMany(t => t.Policies).Take(8))
                    stack.Children.Add(BuildPolicyChip(p, narrow: true));
                if (role.TotalPolicies > 8)
                    stack.Children.Add(new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 4, 0, 0),
                        Child = new TextBlock
                        {
                            Text = $"+{role.TotalPolicies - 8} More",
                            Foreground = Brushes.White, FontSize = 10
                        }
                    });
                RoleCardsHost.Children.Add(card);
            }
        }

        // Used for role cards (titleCommit / descCommit are non-null) AND for
        // table cards inside a role (the click-to-edit fields write back to
        // the underlying TablePolicies). Pass null commits for read-only.
        private Border BuildSummaryCard(string title, string desc, int policyCount, bool isSelected,
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
                Text = $"{Math.Min(policyCount, 999)} Policies",
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
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x4C, 0x4C))
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                Child = new Border { Background = Brushes.Transparent, Child = inner }
            };
        }

        private Border BuildPolicyChip(Policy p, bool narrow)
        {
            var color = CategoryColors.TryGetValue(p.Category, out var c)
                ? c : Color.FromRgb(0xFF, 0x4C, 0x4C);
            return new Border
            {
                Background = new SolidColorBrush(color),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 2, 0, 2),
                CornerRadius = new CornerRadius(2),
                Child = new TextBlock
                {
                    Text = $"{p.Name} - {p.Description}",
                    Foreground = Brushes.White, FontSize = narrow ? 9 : 11,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            };
        }

        // ── Middle: selected role detail ──────────────────────────────────
        private void RebuildSelectedRolePanel()
        {
            TableCardsHost.Children.Clear();
            SelectedRolePolicyChips.Children.Clear();
            if (_selectedRole == null)
            {
                SelectedRoleName.Text = "No role selected";
                SelectedRolePolicyCount.Text = "0 Policies";
                return;
            }
            SelectedRoleName.Text = _selectedRole.Name;
            SelectedRoleDesc.Text = _selectedRole.Description;
            SelectedRolePolicyCount.Text = $"{_selectedRole.TotalPolicies} Policies";

            foreach (var p in _selectedRole.Tables.SelectMany(t => t.Policies).Take(12))
                SelectedRolePolicyChips.Children.Add(BuildPolicyChip(p, narrow: true));

            foreach (var t in _selectedRole.Tables)
            {
                var captured = t;
                var card = BuildTableCard(t, isSelected: t == _selectedTable);
                card.MouseLeftButtonUp += (s, e) =>
                {
                    _selectedTable = captured;
                    RebuildPolicyEditor();
                };
                TableCardsHost.Children.Add(card);
            }
        }

        private Border BuildTableCard(TablePolicies t, bool isSelected)
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
                Text = $"{Math.Min(t.Policies.Count, 999)} Policies",
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
                FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right
            });
            stack.Children.Add(header);

            var capturedT = t;
            var titleEl = EditableTextHelpers.EditableText(
                t.TableName, v => { capturedT.TableName = v; RebuildAll(); },
                fontSize: 13, weight: FontWeights.Bold);
            ((Grid)titleEl).Margin = new Thickness(0, 8, 0, 4);
            stack.Children.Add(titleEl);

            var descEl = EditableTextHelpers.EditableText(
                t.Description, v => { capturedT.Description = v; },
                fontSize: 10, foreground: Color.FromRgb(0x88, 0x88, 0x88),
                wrap: true, maxWidth: 160);
            ((Grid)descEl).Margin = new Thickness(0, 0, 0, 8);
            stack.Children.Add(descEl);

            stack.Children.Add(BuildCategoryBar(t));
            return new Border
            {
                Width = 178, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x4C, 0x4C))
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand,
                Child = stack
            };
        }

        // Stacked horizontal bar showing category usage proportion.
        private StackPanel BuildCategoryBar(TablePolicies t)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            if (t.Policies.Count == 0)
            {
                row.Children.Add(new Border
                {
                    Width = 150, Height = 4,
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A))
                });
                return row;
            }
            foreach (var grp in t.Policies.GroupBy(p => p.Category))
            {
                var color = CategoryColors.TryGetValue(grp.Key, out var c)
                    ? c : Color.FromRgb(0xFF, 0x4C, 0x4C);
                row.Children.Add(new Border
                {
                    Width = 150 * grp.Count() / Math.Max(1, t.Policies.Count),
                    Height = 4, Background = new SolidColorBrush(color)
                });
            }
            return row;
        }

        // ── Bottom: per-table policy editor ───────────────────────────────
        private void RebuildPolicyEditor()
        {
            PolicyRowsHost.Items.Clear();
            EditorTableUsageBars.Children.Clear();
            CategoryLegendHost.Items.Clear();
            foreach (var (label, desc) in new (string, string)[]
            {
                ("Base Server",   "The data required to make the server work at all; delete this and the server ceases to work!"),
                ("Communication", "All data related to messages, chatrooms, servers and organized communication in general."),
                ("Profiles",      "All data related to profile creation, management and change."),
                ("Economy",       "All data related to marketplaces, finances, payments and general economic systems."),
            })
            {
                CategoryLegendHost.Items.Add(BuildCategoryLegendTile(label, desc));
            }
            if (_selectedTable == null)
            {
                EditorTableName.Text = "No table selected";
                EditorTablePolicyCount.Text = "0 Policies";
                EditorTableDesc.Text = "Pick a table above to edit its policies.";
                return;
            }
            EditorTableName.Text = _selectedTable.TableName;
            EditorTableDesc.Text = _selectedTable.Description;
            EditorTablePolicyCount.Text = $"{_selectedTable.Policies.Count} Policies";
            EditorTableUsageBars.Children.Add(BuildCategoryBar(_selectedTable));

            foreach (var p in _selectedTable.Policies)
                PolicyRowsHost.Items.Add(BuildPolicyRow(p));
        }

        private Border BuildPolicyRow(Policy p)
        {
            var color = CategoryColors.TryGetValue(p.Category, out var c)
                ? c : Color.FromRgb(0xFF, 0x4C, 0x4C);
            var captured = p;

            // Single-row Grid: [colour stripe] [name + description] [Open button]
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var swatch = new Border
            {
                Width = 14, Height = 14, CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(swatch, 0);
            rowGrid.Children.Add(swatch);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameEl = EditableTextHelpers.EditableText(
                p.Name, v => { captured.Name = v; }, fontSize: 12, weight: FontWeights.Bold);
            textStack.Children.Add(nameEl);
            var descEl = EditableTextHelpers.EditableText(
                p.Description, v => { captured.Description = v; },
                fontSize: 10, foreground: Color.FromRgb(0x88, 0x88, 0x88), wrap: true);
            ((Grid)descEl).Margin = new Thickness(0, 4, 0, 0);
            textStack.Children.Add(descEl);
            Grid.SetColumn(textStack, 1);
            rowGrid.Children.Add(textStack);

            var openBtn = EditableTextHelpers.OpenInNodeWalkerButton(() =>
            {
                if (HostPage == null) return;
                var slug = $"RLS_{Slug(_selectedRole?.Name)}_{Slug(_selectedTable?.TableName)}_{Slug(captured.Name)}";
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

        // Lower-case alphanum slug for filesystem-safe session naming.
        private static string Slug(string s) =>
            string.IsNullOrWhiteSpace(s) ? "untitled" :
            new string(s.Where(ch => char.IsLetterOrDigit(ch)).ToArray()).ToLowerInvariant();

        private Border BuildCategoryLegendTile(string label, string description)
        {
            var color = CategoryColors.TryGetValue(label, out var c)
                ? c : Color.FromRgb(0xFF, 0x4C, 0x4C);
            var stack = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            head.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 8, Height = 8, Fill = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center
            });
            head.Children.Add(new TextBlock
            {
                Text = label, Foreground = Brushes.White,
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

        // ── Toolbar handlers ──────────────────────────────────────────────
        private void CreateRoleFromInput()
        {
            var n = (NewRoleNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            Roles.Add(new PolicyRole { Name = n });
            NewRoleNameBox.Text = "";
            _selectedRole = Roles.Last();
            RebuildAll();
        }
        private void CreateTableFromInput()
        {
            if (_selectedRole == null) return;
            var n = (NewPolicyTableBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            var t = new TablePolicies { TableName = n };
            _selectedRole.Tables.Add(t);
            NewPolicyTableBox.Text = "";
            _selectedTable = t;
            RebuildAll();
        }
        private void CreatePolicyFromInput()
        {
            if (_selectedTable == null) return;
            var n = (NewPolicyNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            _selectedTable.Policies.Add(new Policy { Name = n });
            NewPolicyNameBox.Text = "";
            RebuildAll();
        }
    }

}
