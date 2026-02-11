using DatabaseDesigner;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Database_Designer
{
    public partial class ConfirmDeleteTable : Page
    {

        public MainPage mainPaged;
        string MainTableName;
        DatabaseViewer Viewer;

        public UIWindowEntry WindowInfo { get; private set; }


        public ConfirmDeleteTable(MainPage mainpage, string mainTableName, DatabaseViewer viewer)
        {
            this.InitializeComponent();
            mainPaged = mainpage;
            MainTableName = mainTableName;
            Viewer = viewer;

            string baseUrl = mainpage.baseUrl;


            No.Click += (s, e) =>
            {
                ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };
            };



            Yes.Click += async (s, e) =>
            {

                ExecuteTableDeletion();
            };






            ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };



            this.Unloaded += (s, e) =>
            {
                RemoveWindow();
            };



        }



        private void ExecuteTableDeletion()
        {
            // Find the table to delete
            var parts = MainTableName.Split('.');
            string tableName = parts[^1];
            string schemaName = parts.Length > 1 ? string.Join(".", parts[..^1]) : "public";

            SessionStorage.TableObject? selectedTable = mainPaged.MainSessionInfo.Tables
                .FirstOrDefault(t =>
                    string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(t.SchemaName?.Trim('"') ?? "public", schemaName.Trim('"'), StringComparison.OrdinalIgnoreCase));

            if (selectedTable != null)
            {
                mainPaged.MainSessionInfo.Tables.Remove((SessionStorage.TableObject)selectedTable);
                mainPaged.ForceCollectionChangeUpate();
            }

            // Close the viewer window
            if (mainPaged.IntroPage.Children.Contains(this))
            {
                mainPaged.IntroPage.Children.Remove(this);
                mainPaged.IntroPage.Children.Remove(Viewer);

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
