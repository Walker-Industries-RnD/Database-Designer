using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static Database_Designer.NodeWalker.NodeWalker.Node;

namespace Database_Designer.NodeWalker
{
    public partial class NodeLibraryWindow : Window
    {
        public event EventHandler<BareNode> NodeSelected;

        private readonly List<Category> _allCategories;
        private List<Category> _filteredCategories;

        public NodeLibraryWindow(List<Category> categories)
        {
            InitializeComponent();
            _allCategories = categories ?? new List<Category>();
            _filteredCategories = new List<Category>(_allCategories);

            SearchBox.TextChanged += SearchBox_TextChanged;
            UpdateWatermark();

            LoadTree();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateWatermark();

            string filter = SearchBox.Text?.Trim().ToLower() ?? "";

            if (string.IsNullOrEmpty(filter))
            {
                _filteredCategories = new List<Category>(_allCategories);
            }
            else
            {
                _filteredCategories = _allCategories
                    .Select(cat => new Category
                    {
                        Name = cat.Name,
                        Nodes = cat.Nodes.Where(n =>
                            (n.Title?.ToLower().Contains(filter) ?? false) ||
                            (n.Description?.ToLower().Contains(filter) ?? false))
                            .ToList()
                    })
                    .Where(cat => cat.Nodes.Any())
                    .ToList();
            }

            LoadTree();
        }

        private void UpdateWatermark()
        {
            SearchWatermark.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void LoadTree()
        {
            LibraryTree.Items.Clear();

            foreach (var cat in _filteredCategories)
            {
                var catItem = new TreeViewItem
                {
                    Header = cat.Name,
                    IsExpanded = true,
                    FontSize = 15,
                    Foreground = Brushes.White,
                    Background = Brushes.Transparent
                };

                foreach (var node in cat.Nodes)
                {
                    var nodeItem = new TreeViewItem
                    {
                        Header = node.Title,
                        Tag = node,
                        Foreground = Brushes.White,
                        Background = Brushes.Transparent
                    };
                    catItem.Items.Add(nodeItem);
                }

                LibraryTree.Items.Add(catItem);
            }
        }

        private void LibraryTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LibraryTree.SelectedItem is TreeViewItem item && item.Tag is BareNode node)
            {
                NodeSelected?.Invoke(this, node);
                Close();
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryTree.SelectedItem is TreeViewItem item && item.Tag is BareNode node)
            {
                NodeSelected?.Invoke(this, node);
                Close();
            }
            else
            {

            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}